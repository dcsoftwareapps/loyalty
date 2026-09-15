using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoyaltyCloud.Cashier.Services;

public interface IRewardRedemptionPendingStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
}

public sealed record PendingRewardRedemption(
    string Account,
    string SerialNumber,
    Guid RewardCatalogItemId,
    string RewardName,
    int PointsCost,
    string IdempotencyKey,
    CashierRedemptionResponse? CreatedRedemption = null);

public sealed class RewardRedemptionCoordinator(
    ICashierRewardRedemptionApi api,
    IRewardRedemptionPendingStore store,
    Func<CashierSession?> currentSession,
    string server)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Account(CashierSession session) =>
        server.TrimEnd('/') + "|" + session.TenantSlug.ToLowerInvariant() + "|" + session.UserId.ToString("N");

    private static string StorageKey(string account) => "loyaltycloud.cashier.reward.pending.v1."
        + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account)));

    private bool IsCurrent(CashierSession session) => currentSession() is { } active
        && active == session && !active.IsExpired(DateTimeOffset.UtcNow);

    public async Task<PendingRewardRedemption?> LoadAsync()
    {
        await gate.WaitAsync();
        try
        {
            var session = currentSession();
            if (session is null || !IsCurrent(session))
                return null;

            var pending = await ReadAsync(Account(session));
            return IsCurrent(session) ? pending : null;
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<CashierOperationResult<CashierRedemptionResponse>> BeginAsync(
        string serialNumber,
        CashierRewardCatalogItem reward) =>
        SendAsync(serialNumber, reward, retry: false);

    public Task<CashierOperationResult<CashierRedemptionResponse>> RetryAsync() =>
        SendAsync(null, null, retry: true);

    public async Task ClearAsync()
    {
        await gate.WaitAsync();
        try
        {
            var session = currentSession();
            if (session is null || !IsCurrent(session))
                return;

            await store.SetAsync(StorageKey(Account(session)), string.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<PendingRewardRedemption?> ReadAsync(string account)
    {
        var json = await store.GetAsync(StorageKey(account));
        if (string.IsNullOrEmpty(json))
            return null;

        var pending = JsonSerializer.Deserialize<PendingRewardRedemption>(json, JsonOptions);
        if (pending is null
            || pending.Account != account
            || string.IsNullOrWhiteSpace(pending.SerialNumber)
            || pending.RewardCatalogItemId == Guid.Empty
            || pending.PointsCost <= 0
            || string.IsNullOrWhiteSpace(pending.IdempotencyKey)
            || (pending.CreatedRedemption is not null
                && (pending.CreatedRedemption.RedemptionId == Guid.Empty
                    || pending.CreatedRedemption.PointsSpent != pending.PointsCost)))
        {
            throw new InvalidOperationException("No se puede verificar el canje pendiente.");
        }

        return pending;
    }

    private async Task<CashierOperationResult<CashierRedemptionResponse>> SendAsync(
        string? serialNumber,
        CashierRewardCatalogItem? reward,
        bool retry)
    {
        if (!await gate.WaitAsync(0))
            return CashierOperationResult<CashierRedemptionResponse>.Failure("Ya hay una operación en curso.");

        try
        {
            var session = currentSession();
            if (session is null || !IsCurrent(session))
                return Unauthorized();

            var account = Account(session);
            var pending = await ReadAsync(account);

            if (retry && pending is null)
                return CashierOperationResult<CashierRedemptionResponse>.Failure("No hay un canje pendiente.");

            if (!retry)
            {
                if (pending is not null)
                    return CashierOperationResult<CashierRedemptionResponse>.Failure("Primero resuelve el canje pendiente.");

                if (string.IsNullOrWhiteSpace(serialNumber) || reward is null || !reward.CanAfford || reward.Id == Guid.Empty)
                    return CashierOperationResult<CashierRedemptionResponse>.Failure("Selecciona una recompensa válida.");

                pending = new(
                    account,
                    serialNumber.Trim(),
                    reward.Id,
                    reward.Name,
                    reward.PointsCost,
                    Guid.NewGuid().ToString("N"));

                // Persist BEFORE the first HTTP request so a retry reuses the same logical redemption.
                await store.SetAsync(StorageKey(account), JsonSerializer.Serialize(pending, JsonOptions));
            }

            if (!IsCurrent(session))
                return Unauthorized();

            var result = await api.RedeemRewardAsync(
                pending!.SerialNumber,
                pending.RewardCatalogItemId,
                pending.IdempotencyKey);

            if (result.Succeeded && result.Value is not null)
            {
                var response = result.Value;
                if (response.RedemptionId == Guid.Empty || response.PointsSpent != pending.PointsCost)
                {
                    return CashierOperationResult<CashierRedemptionResponse>.UncertainFailure(
                        "La respuesta no confirma este canje. Reintenta la operación pendiente.");
                }

                pending = pending with { CreatedRedemption = response };
                await store.SetAsync(StorageKey(account), JsonSerializer.Serialize(pending, JsonOptions));
            }
            else if (!retry && !result.Uncertain)
            {
                // A direct validation rejection on the first attempt is known not to have debited.
                await store.SetAsync(StorageKey(account), string.Empty);
            }

            return IsCurrent(session) ? result : Unauthorized();
        }
        catch (Exception)
        {
            // Fail closed on unreadable storage, interrupted writes or unexpected transport errors.
            // Never erase the record or generate another key to recover.
            return CashierOperationResult<CashierRedemptionResponse>.UncertainFailure(
                "No se pudo confirmar o guardar el estado del canje. No repitas el canje como una operación nueva; vuelve a intentar con esta cuenta.");
        }
        finally
        {
            gate.Release();
        }
    }

    private static CashierOperationResult<CashierRedemptionResponse> Unauthorized() =>
        new(false, null, "Inicia sesión con la misma cuenta para resolver el canje pendiente.", Unauthorized: true);
}
