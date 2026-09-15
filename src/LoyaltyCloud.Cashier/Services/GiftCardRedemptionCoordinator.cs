using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoyaltyCloud.Cashier.Services;

public interface IGiftCardPendingStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
}

public sealed record PendingGiftCardRedemption(string Account, string Code, GiftCardRedemption Request);

public sealed class GiftCardRedemptionCoordinator(
    ICashierGiftCardApi api, IGiftCardPendingStore store, Func<CashierSession?> currentSession, string server)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Account(CashierSession session) =>
        server.TrimEnd('/') + "|" + session.TenantSlug.ToLowerInvariant() + "|" + session.UserId.ToString("N");
    private static string StorageKey(string account) => "loyaltycloud.cashier.gift.pending.v1." +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account)));
    private bool IsCurrent(CashierSession session) => currentSession() is { } active
        && active == session && !active.IsExpired(DateTimeOffset.UtcNow);

    public async Task<PendingGiftCardRedemption?> LoadAsync()
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

    public Task<GiftCardResult<GiftCardReceipt>> BeginAsync(string code, decimal amount) => SendAsync(code, amount, false);
    public Task<GiftCardResult<GiftCardReceipt>> RetryAsync() => SendAsync(null, 0m, true);

    private async Task<PendingGiftCardRedemption?> ReadAsync(string account)
    {
        var json = await store.GetAsync(StorageKey(account));
        if (string.IsNullOrEmpty(json)) return null;
        var pending = JsonSerializer.Deserialize<PendingGiftCardRedemption>(json, JsonOptions);
        if (pending is null || pending.Account != account || pending.Request is null
            || GiftCardQrParser.Parse(pending.Code)?.Code != pending.Code
            || pending.Request.Amount <= 0 || string.IsNullOrWhiteSpace(pending.Request.IdempotencyKey))
            throw new InvalidOperationException("No se puede verificar el canje pendiente.");
        return pending;
    }

    private async Task<GiftCardResult<GiftCardReceipt>> SendAsync(string? code, decimal amount, bool retry)
    {
        if (!await gate.WaitAsync(0)) return GiftCardResult<GiftCardReceipt>.Fail("Busy", "Ya hay una operación en curso.");
        try
        {
            var session = currentSession();
            if (session is null || !IsCurrent(session)) return Unauthorized();
            var account = Account(session);
            var pending = await ReadAsync(account);
            if (retry && pending is null)
                return GiftCardResult<GiftCardReceipt>.Fail("NoPending", "No hay un canje pendiente.");
            if (!retry)
            {
                if (pending is not null)
                    return GiftCardResult<GiftCardReceipt>.Fail("Pending", "Primero resuelve el canje pendiente.");
                var normalized = GiftCardQrParser.Parse(code)?.Code;
                if (normalized is null || amount <= 0)
                    return GiftCardResult<GiftCardReceipt>.Fail("InvalidInput", "Revisa el código y el monto.");
                pending = new(account, normalized, new(amount, Guid.NewGuid().ToString("N")));
                // Persist BEFORE any HTTP request. A crash from this point must reuse the same payload.
                await store.SetAsync(StorageKey(account), JsonSerializer.Serialize(pending, JsonOptions));
            }
            if (!IsCurrent(session)) return Unauthorized();
            var result = await api.RedeemAsync(pending!.Code, pending.Request);
            if (result.Succeeded)
            {
                var receipt = result.Value!;
                if (receipt.Card is null || receipt.Card.Code != pending.Code || receipt.RedeemedAmount != pending.Request.Amount
                    || receipt.Card.RemainingBalance < 0 || string.IsNullOrWhiteSpace(receipt.Card.Status)
                    || string.IsNullOrWhiteSpace(receipt.Card.Currency))
                    return GiftCardResult<GiftCardReceipt>.Fail("Uncertain", "La respuesta no confirma este canje. Reintenta la operación pendiente.");
                await store.SetAsync(StorageKey(account), string.Empty);
            }
            else if (!retry && result.DefinitiveRejection)
            {
                // A direct rejection of the FIRST attempt is known not to have debited.
                // After uncertainty, even a rejection can hide an earlier committed request.
                await store.SetAsync(StorageKey(account), string.Empty);
            }
            return IsCurrent(session) ? result : Unauthorized();
        }
        catch (Exception)
        {
            // Fail closed on unreadable storage, interrupted writes or unexpected transport errors.
            // Never erase the record or generate another key to recover.
            return GiftCardResult<GiftCardReceipt>.Fail("StorageOrUncertain",
                "No se pudo confirmar o guardar el estado del canje. No repitas el cobro como una operación nueva; vuelve a intentar con esta cuenta.");
        }
        finally { gate.Release(); }
    }

    private static GiftCardResult<GiftCardReceipt> Unauthorized() => GiftCardResult<GiftCardReceipt>.Fail(
        "Unauthorized", "Inicia sesión con la misma cuenta para resolver el canje pendiente.");
}
