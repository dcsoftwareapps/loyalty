using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoyaltyCloud.Cashier.Services;

public interface IMonetaryRedemptionPendingStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
}

public sealed record PendingMonetaryRedemption(
    string Account,
    string SerialNumber,
    int PointsToRedeem,
    decimal MonetaryAmount,
    string MonetaryCurrency,
    decimal MonetaryPointsPerPesoUnit,
    string IdempotencyKey,
    CashierRedemptionResponse? CreatedRedemption = null);

public sealed class MonetaryRedemptionCoordinator(
    ICashierMonetaryRedemptionApi api,
    IMonetaryRedemptionPendingStore store,
    Func<CashierSession?> currentSession,
    string server)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Account(CashierSession session) =>
        server.TrimEnd('/') + "|" + session.TenantSlug.ToLowerInvariant() + "|" + session.UserId.ToString("N");

    private static string StorageKey(string account) => "loyaltycloud.cashier.monetary.pending.v1."
        + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account)));

    private bool IsCurrent(CashierSession session) => currentSession() is { } active
        && active == session && !active.IsExpired(DateTimeOffset.UtcNow);

    public async Task<PendingMonetaryRedemption?> LoadAsync()
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
        CashierMonetaryRedemptionPreview preview) =>
        SendAsync(preview, retry: false);

    public Task<CashierOperationResult<CashierRedemptionResponse>> RetryAsync() =>
        SendAsync(null, retry: true);

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

    private async Task<PendingMonetaryRedemption?> ReadAsync(string account)
    {
        var json = await store.GetAsync(StorageKey(account));
        if (string.IsNullOrEmpty(json))
            return null;

        var pending = JsonSerializer.Deserialize<PendingMonetaryRedemption>(json, JsonOptions);
        if (pending is null
            || pending.Account != account
            || string.IsNullOrWhiteSpace(pending.SerialNumber)
            || pending.PointsToRedeem <= 0
            || pending.MonetaryAmount <= 0
            || string.IsNullOrWhiteSpace(pending.MonetaryCurrency)
            || pending.MonetaryPointsPerPesoUnit <= 0
            || string.IsNullOrWhiteSpace(pending.IdempotencyKey)
            || (pending.CreatedRedemption is not null
                && (pending.CreatedRedemption.RedemptionId == Guid.Empty
                    || pending.CreatedRedemption.PointsSpent != pending.PointsToRedeem
                    || pending.CreatedRedemption.MonetaryAmount != pending.MonetaryAmount
                    || !string.Equals(pending.CreatedRedemption.MonetaryCurrency, pending.MonetaryCurrency, StringComparison.OrdinalIgnoreCase)
                    || pending.CreatedRedemption.MonetaryPointsPerPesoUnit != pending.MonetaryPointsPerPesoUnit)))
        {
            throw new InvalidOperationException("No se puede verificar el descuento pendiente.");
        }

        return pending;
    }

    private async Task<CashierOperationResult<CashierRedemptionResponse>> SendAsync(
        CashierMonetaryRedemptionPreview? preview,
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
                return CashierOperationResult<CashierRedemptionResponse>.Failure("No hay un descuento pendiente.");

            if (!retry)
            {
                if (pending is not null)
                    return CashierOperationResult<CashierRedemptionResponse>.Failure("Primero resuelve el descuento pendiente.");

                if (preview is null
                    || preview.PointsToRedeem <= 0
                    || preview.MonetaryAmount <= 0
                    || preview.MonetaryPointsPerPesoUnit <= 0
                    || string.IsNullOrWhiteSpace(preview.SerialNumber)
                    || string.IsNullOrWhiteSpace(preview.MonetaryCurrency))
                    return CashierOperationResult<CashierRedemptionResponse>.Failure("Recalcula el descuento antes de continuar.");

                pending = new(
                    account,
                    preview.SerialNumber.Trim(),
                    preview.PointsToRedeem,
                    preview.MonetaryAmount,
                    preview.MonetaryCurrency,
                    preview.MonetaryPointsPerPesoUnit,
                    Guid.NewGuid().ToString("N"));

                // Persist BEFORE the first HTTP request so a retry reuses the same logical discount.
                await store.SetAsync(StorageKey(account), JsonSerializer.Serialize(pending, JsonOptions));
            }

            if (!IsCurrent(session))
                return Unauthorized();

            var requestPreview = new CashierMonetaryRedemptionPreview(
                pending!.SerialNumber,
                pending.PointsToRedeem,
                pending.MonetaryAmount,
                pending.MonetaryCurrency,
                pending.MonetaryPointsPerPesoUnit,
                pending.PointsToRedeem,
                0);
            var result = await api.RedeemMonetaryAsync(requestPreview, pending.IdempotencyKey);

            if (result.Succeeded && result.Value is not null)
            {
                var response = result.Value;
                if (response.RedemptionId == Guid.Empty
                    || response.PointsSpent != pending.PointsToRedeem
                    || response.MonetaryAmount != pending.MonetaryAmount
                    || !string.Equals(response.MonetaryCurrency, pending.MonetaryCurrency, StringComparison.OrdinalIgnoreCase)
                    || response.MonetaryPointsPerPesoUnit != pending.MonetaryPointsPerPesoUnit)
                {
                    return CashierOperationResult<CashierRedemptionResponse>.UncertainFailure(
                        "La respuesta no confirma este descuento. Reintenta la operación pendiente.");
                }

                pending = pending with { CreatedRedemption = response };
                await store.SetAsync(StorageKey(account), JsonSerializer.Serialize(pending, JsonOptions));
            }
            else if (!retry && !result.Uncertain)
            {
                await store.SetAsync(StorageKey(account), string.Empty);
            }

            return IsCurrent(session) ? result : Unauthorized();
        }
        catch (Exception)
        {
            return CashierOperationResult<CashierRedemptionResponse>.UncertainFailure(
                "No se pudo confirmar o guardar el estado del descuento. No repitas el canje como una operación nueva; vuelve a intentar con esta cuenta.");
        }
        finally
        {
            gate.Release();
        }
    }

    private static CashierOperationResult<CashierRedemptionResponse> Unauthorized() =>
        new(false, null, "Inicia sesión con la misma cuenta para resolver el descuento pendiente.", Unauthorized: true);
}
