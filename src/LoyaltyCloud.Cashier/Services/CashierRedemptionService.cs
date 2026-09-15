using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoyaltyCloud.Cashier.Services;

public interface ICashierRewardRedemptionApi
{
    Task<CashierOperationResult<CashierRedemptionResponse>> RedeemRewardAsync(
        string serialNumber,
        Guid rewardCatalogItemId,
        string idempotencyKey,
        CancellationToken ct = default);
}

public interface ICashierMonetaryRedemptionApi
{
    Task<CashierOperationResult<CashierMonetaryRedemptionPreview>> PreviewMonetaryAsync(
        string serialNumber,
        int pointsToRedeem,
        CancellationToken ct = default);

    Task<CashierOperationResult<CashierRedemptionResponse>> RedeemMonetaryAsync(
        CashierMonetaryRedemptionPreview preview,
        string idempotencyKey,
        CancellationToken ct = default);
}

public sealed class CashierRedemptionService : ICashierRewardRedemptionApi, ICashierMonetaryRedemptionApi
{
    private readonly AuthenticatedCashierApiClient _api;

    public CashierRedemptionService(AuthenticatedCashierApiClient api)
    {
        _api = api;
    }

    public async Task<CashierOperationResult<IReadOnlyList<CashierRewardCatalogItem>>> GetCatalogAsync(
        string serialNumber,
        CancellationToken ct = default)
    {
        var serial = serialNumber.Trim();
        if (string.IsNullOrWhiteSpace(serial))
            return CashierOperationResult<IReadOnlyList<CashierRewardCatalogItem>>.Failure("Selecciona un cliente.");

        try
        {
            using var response = await _api.GetAsync($"api/redemptions/catalog/{Uri.EscapeDataString(serial)}", ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return CashierOperationResult<IReadOnlyList<CashierRewardCatalogItem>>.SessionExpired();

            if (!response.IsSuccessStatusCode)
                return CashierOperationResult<IReadOnlyList<CashierRewardCatalogItem>>.Failure(await ReadApiErrorAsync(response, ct));

            var catalog = await response.Content.ReadFromJsonAsync<IReadOnlyList<CashierRewardCatalogItem>>(cancellationToken: ct);
            return catalog is null
                ? CashierOperationResult<IReadOnlyList<CashierRewardCatalogItem>>.Failure("La respuesta del catálogo no fue válida.")
                : CashierOperationResult<IReadOnlyList<CashierRewardCatalogItem>>.Success(catalog);
        }
        catch (HttpRequestException)
        {
            return CashierOperationResult<IReadOnlyList<CashierRewardCatalogItem>>.Failure("No hay conexión con LoyaltyCloud. Revisa tu internet e intenta de nuevo.");
        }
        catch (TaskCanceledException)
        {
            return CashierOperationResult<IReadOnlyList<CashierRewardCatalogItem>>.Failure("La conexión tardó demasiado. Intenta de nuevo.");
        }
    }

    public async Task<CashierOperationResult<CashierRedemptionResponse>> RedeemRewardAsync(
        string serialNumber,
        Guid rewardCatalogItemId,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var serial = serialNumber.Trim();
        if (string.IsNullOrWhiteSpace(serial))
            return CashierOperationResult<CashierRedemptionResponse>.Failure("Selecciona un cliente.");

        if (rewardCatalogItemId == Guid.Empty)
            return CashierOperationResult<CashierRedemptionResponse>.Failure("Selecciona una recompensa.");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return CashierOperationResult<CashierRedemptionResponse>.Failure("No se pudo preparar el canje de forma segura.");

        try
        {
            using var response = await _api.PostAsJsonAsync(
                "api/redemptions",
                new CashierRedeemRewardRequest(serial, rewardCatalogItemId, IdempotencyKey: idempotencyKey.Trim()),
                ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return CashierOperationResult<CashierRedemptionResponse>.SessionExpired();

            if ((int)response.StatusCode >= 500)
                return CashierOperationResult<CashierRedemptionResponse>.UncertainFailure(
                    "No se pudo confirmar si el canje fue registrado. Reintenta la operación pendiente.");

            if (!response.IsSuccessStatusCode)
                return CashierOperationResult<CashierRedemptionResponse>.Failure(await ReadApiErrorAsync(response, ct));

            var result = await response.Content.ReadFromJsonAsync<CashierRedemptionResponse>(cancellationToken: ct);
            return result is null
                ? CashierOperationResult<CashierRedemptionResponse>.UncertainFailure("La respuesta del canje no fue válida. Reintenta la operación pendiente.")
                : CashierOperationResult<CashierRedemptionResponse>.Success(result);
        }
        catch (HttpRequestException)
        {
            return CashierOperationResult<CashierRedemptionResponse>.UncertainFailure("No hay conexión con LoyaltyCloud. Reintenta la operación pendiente.");
        }
        catch (TaskCanceledException)
        {
            return CashierOperationResult<CashierRedemptionResponse>.UncertainFailure("La conexión tardó demasiado. Reintenta la operación pendiente.");
        }
    }

    public async Task<CashierOperationResult<CashierMonetaryRedemptionPreview>> PreviewMonetaryAsync(
        string serialNumber,
        int pointsToRedeem,
        CancellationToken ct = default)
    {
        var serial = serialNumber.Trim();
        if (string.IsNullOrWhiteSpace(serial))
            return CashierOperationResult<CashierMonetaryRedemptionPreview>.Failure("Selecciona un cliente.");

        if (pointsToRedeem <= 0)
            return CashierOperationResult<CashierMonetaryRedemptionPreview>.Failure("Ingresa los puntos a canjear.");

        try
        {
            using var response = await _api.PostAsJsonAsync(
                "api/redemptions/monetary/preview",
                new CashierMonetaryPreviewRequest(serial, pointsToRedeem),
                ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return CashierOperationResult<CashierMonetaryRedemptionPreview>.SessionExpired();

            if (!response.IsSuccessStatusCode)
                return CashierOperationResult<CashierMonetaryRedemptionPreview>.Failure(await ReadApiErrorAsync(response, ct));

            var result = await response.Content.ReadFromJsonAsync<CashierMonetaryRedemptionPreview>(cancellationToken: ct);
            return result is null
                ? CashierOperationResult<CashierMonetaryRedemptionPreview>.Failure("La respuesta del descuento no fue válida.")
                : CashierOperationResult<CashierMonetaryRedemptionPreview>.Success(result);
        }
        catch (HttpRequestException)
        {
            return CashierOperationResult<CashierMonetaryRedemptionPreview>.Failure("No hay conexión con LoyaltyCloud. Revisa tu internet e intenta de nuevo.");
        }
        catch (TaskCanceledException)
        {
            return CashierOperationResult<CashierMonetaryRedemptionPreview>.Failure("La conexión tardó demasiado. Intenta de nuevo.");
        }
    }

    public async Task<CashierOperationResult<CashierRedemptionResponse>> RedeemMonetaryAsync(
        CashierMonetaryRedemptionPreview preview,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (preview.PointsToRedeem <= 0 || string.IsNullOrWhiteSpace(preview.SerialNumber))
            return CashierOperationResult<CashierRedemptionResponse>.Failure("Recalcula el descuento antes de continuar.");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return CashierOperationResult<CashierRedemptionResponse>.Failure("No se pudo preparar el canje de forma segura.");

        try
        {
            using var response = await _api.PostAsJsonAsync(
                "api/redemptions",
                new CashierRedeemRewardRequest(
                    preview.SerialNumber.Trim(),
                    null,
                    Type: "MonetaryDiscount",
                    PointsToRedeem: preview.PointsToRedeem,
                    IdempotencyKey: idempotencyKey.Trim(),
                    MonetaryAmount: preview.MonetaryAmount,
                    MonetaryCurrency: preview.MonetaryCurrency,
                    MonetaryPointsPerPesoUnit: preview.MonetaryPointsPerPesoUnit),
                ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return CashierOperationResult<CashierRedemptionResponse>.SessionExpired();

            if ((int)response.StatusCode >= 500)
                return CashierOperationResult<CashierRedemptionResponse>.UncertainFailure(
                    "No se pudo confirmar si el descuento fue registrado. Reintenta la operación pendiente.");

            if (!response.IsSuccessStatusCode)
                return CashierOperationResult<CashierRedemptionResponse>.Failure(await ReadApiErrorAsync(response, ct));

            var result = await response.Content.ReadFromJsonAsync<CashierRedemptionResponse>(cancellationToken: ct);
            return result is null
                ? CashierOperationResult<CashierRedemptionResponse>.UncertainFailure("La respuesta del descuento no fue válida. Reintenta la operación pendiente.")
                : CashierOperationResult<CashierRedemptionResponse>.Success(result);
        }
        catch (HttpRequestException)
        {
            return CashierOperationResult<CashierRedemptionResponse>.UncertainFailure("No hay conexión con LoyaltyCloud. Reintenta la operación pendiente.");
        }
        catch (TaskCanceledException)
        {
            return CashierOperationResult<CashierRedemptionResponse>.UncertainFailure("La conexión tardó demasiado. Reintenta la operación pendiente.");
        }
    }

    public async Task<CashierOperationResult<bool>> ConfirmAsync(Guid redemptionId, CancellationToken ct = default)
    {
        if (redemptionId == Guid.Empty)
            return CashierOperationResult<bool>.Failure("No hay canje pendiente por confirmar.");

        try
        {
            using var response = await _api.PutAsJsonAsync(
                $"api/redemptions/{redemptionId}/confirm",
                new CashierRedemptionActionRequest(null),
                ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return CashierOperationResult<bool>.SessionExpired();

            return response.IsSuccessStatusCode
                ? CashierOperationResult<bool>.Success(true)
                : CashierOperationResult<bool>.Failure(await ReadApiErrorAsync(response, ct));
        }
        catch (HttpRequestException)
        {
            return CashierOperationResult<bool>.Failure("No hay conexión con LoyaltyCloud. Revisa tu internet e intenta de nuevo.");
        }
        catch (TaskCanceledException)
        {
            return CashierOperationResult<bool>.Failure("La conexión tardó demasiado. Intenta de nuevo.");
        }
    }

    public async Task<CashierOperationResult<CashierCancelRedemptionResponse>> CancelAsync(
        Guid redemptionId,
        CancellationToken ct = default)
    {
        if (redemptionId == Guid.Empty)
            return CashierOperationResult<CashierCancelRedemptionResponse>.Failure("No hay canje pendiente por cancelar.");

        try
        {
            using var response = await _api.PutAsJsonAsync(
                $"api/redemptions/{redemptionId}/cancel",
                new CashierRedemptionActionRequest("Cancelado desde caja móvil."),
                ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return CashierOperationResult<CashierCancelRedemptionResponse>.SessionExpired();

            if (!response.IsSuccessStatusCode)
                return CashierOperationResult<CashierCancelRedemptionResponse>.Failure(await ReadApiErrorAsync(response, ct));

            var result = await response.Content.ReadFromJsonAsync<CashierCancelRedemptionResponse>(cancellationToken: ct);
            return result is null
                ? CashierOperationResult<CashierCancelRedemptionResponse>.Failure("La respuesta de cancelación no fue válida.")
                : CashierOperationResult<CashierCancelRedemptionResponse>.Success(result);
        }
        catch (HttpRequestException)
        {
            return CashierOperationResult<CashierCancelRedemptionResponse>.Failure("No hay conexión con LoyaltyCloud. Revisa tu internet e intenta de nuevo.");
        }
        catch (TaskCanceledException)
        {
            return CashierOperationResult<CashierCancelRedemptionResponse>.Failure("La conexión tardó demasiado. Intenta de nuevo.");
        }
    }

    private static async Task<string> ReadApiErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text))
                return "No fue posible completar el canje. Intenta de nuevo.";

            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("detail", out var detail)
                && !string.IsNullOrWhiteSpace(detail.GetString()))
                return detail.GetString()!;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
        }

        return "No fue posible completar el canje. Intenta de nuevo.";
    }
}
