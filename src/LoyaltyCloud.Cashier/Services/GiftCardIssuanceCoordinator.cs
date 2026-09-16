using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoyaltyCloud.Cashier.Services;

public interface IGiftCardIssuancePendingStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
}

public sealed record PendingGiftCardIssuance(string Account, GiftCardIssueRequest Request);

public sealed class GiftCardIssuanceCoordinator(
    ICashierGiftCardApi api, IGiftCardIssuancePendingStore store, Func<CashierSession?> currentSession, string server)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Account(CashierSession session) =>
        server.TrimEnd('/') + "|" + session.TenantSlug.ToLowerInvariant() + "|" + session.UserId.ToString("N");
    private static string StorageKey(string account) => "loyaltycloud.cashier.gift.issue.pending.v1." +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account)));
    private bool IsCurrent(CashierSession session) => currentSession() is { } active
        && active == session && !active.IsExpired(DateTimeOffset.UtcNow);

    public async Task<PendingGiftCardIssuance?> LoadAsync()
    {
        await gate.WaitAsync();
        try
        {
            var session = currentSession();
            if (session is null || !IsCurrent(session)) return null;
            var pending = await ReadAsync(Account(session));
            return IsCurrent(session) ? pending : null;
        }
        finally { gate.Release(); }
    }

    public Task<GiftCardResult<GiftCardIssueReceipt>> BeginAsync(GiftCardIssueDraft draft) =>
        SendAsync(draft, false);

    public Task<GiftCardResult<GiftCardIssueReceipt>> RetryAsync() =>
        SendAsync(null, true);

    private async Task<PendingGiftCardIssuance?> ReadAsync(string account)
    {
        var json = await store.GetAsync(StorageKey(account));
        if (string.IsNullOrEmpty(json)) return null;
        var pending = JsonSerializer.Deserialize<PendingGiftCardIssuance>(json, JsonOptions);
        if (pending is null || pending.Account != account || pending.Request is null
            || pending.Request.Amount <= 0 || string.IsNullOrWhiteSpace(pending.Request.RecipientName)
            || string.IsNullOrWhiteSpace(pending.Request.IdempotencyKey))
            throw new InvalidOperationException("No se puede verificar la emisión pendiente.");
        return pending;
    }

    private async Task<GiftCardResult<GiftCardIssueReceipt>> SendAsync(GiftCardIssueDraft? draft, bool retry)
    {
        if (!await gate.WaitAsync(0)) return GiftCardResult<GiftCardIssueReceipt>.Fail("Busy", "Ya hay una operación en curso.");
        try
        {
            var session = currentSession();
            if (session is null || !IsCurrent(session)) return Unauthorized();
            var account = Account(session);
            var pending = await ReadAsync(account);
            if (retry && pending is null)
                return GiftCardResult<GiftCardIssueReceipt>.Fail("NoPending", "No hay una emisión pendiente.");
            if (!retry)
            {
                if (pending is not null)
                    return GiftCardResult<GiftCardIssueReceipt>.Fail("Pending", "Primero resuelve la emisión pendiente.");
                if (draft is null || draft.Amount <= 0 || string.IsNullOrWhiteSpace(draft.RecipientName))
                    return GiftCardResult<GiftCardIssueReceipt>.Fail("InvalidInput", "Revisa el monto y destinatario.", true);

                var request = new GiftCardIssueRequest(draft.Amount, draft.RecipientName.Trim(),
                    Clean(draft.RecipientEmail), Clean(draft.SenderName), Clean(draft.PersonalMessage),
                    draft.ExpiresAtUtc, Guid.NewGuid().ToString("N"));
                pending = new(account, request);
                await store.SetAsync(StorageKey(account), JsonSerializer.Serialize(pending, JsonOptions));
            }
            if (!IsCurrent(session)) return Unauthorized();
            var result = await api.IssueAsync(pending!.Request);
            if (result.Succeeded)
            {
                var receipt = result.Value!;
                if (receipt.Card is null || receipt.Card.InitialBalance != pending.Request.Amount
                    || receipt.Card.RemainingBalance != pending.Request.Amount
                    || string.IsNullOrWhiteSpace(receipt.Card.Code)
                    || string.IsNullOrWhiteSpace(receipt.Card.Currency)
                    || string.IsNullOrWhiteSpace(receipt.Card.Status))
                    return GiftCardResult<GiftCardIssueReceipt>.Fail("Uncertain", "La respuesta no confirma esta emisión. Reintenta la operación pendiente.");
                await store.SetAsync(StorageKey(account), string.Empty);
            }
            else if (!retry && result.DefinitiveRejection)
            {
                await store.SetAsync(StorageKey(account), string.Empty);
            }
            return IsCurrent(session) ? result : Unauthorized();
        }
        catch (Exception)
        {
            return GiftCardResult<GiftCardIssueReceipt>.Fail("StorageOrUncertain",
                "No se pudo confirmar o guardar el estado de la emisión. No repitas la venta como una operación nueva; vuelve a intentar con esta cuenta.");
        }
        finally { gate.Release(); }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static GiftCardResult<GiftCardIssueReceipt> Unauthorized() => GiftCardResult<GiftCardIssueReceipt>.Fail(
        "Unauthorized", "Inicia sesión con la misma cuenta para resolver la emisión pendiente.");
}
