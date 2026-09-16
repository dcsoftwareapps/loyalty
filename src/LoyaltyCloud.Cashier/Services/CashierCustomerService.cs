using System.Net;
using System.Net.Http.Json;

namespace LoyaltyCloud.Cashier.Services;

public sealed class CashierCustomerService
{
    private readonly AuthenticatedCashierApiClient _api;

    public CashierCustomerService(AuthenticatedCashierApiClient api)
    {
        _api = api;
    }

    public async Task<CashierOperationResult<CashierCustomerDetail>> GetCustomerAsync(
        string serialNumber,
        CancellationToken ct = default)
    {
        var serial = serialNumber.Trim();
        if (string.IsNullOrWhiteSpace(serial))
            return CashierOperationResult<CashierCustomerDetail>.Failure("Ingresa el ID del cliente.");

        try
        {
            using var response = await _api.GetAsync($"api/customers/{Uri.EscapeDataString(serial)}", ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return CashierOperationResult<CashierCustomerDetail>.SessionExpired();

            if (response.StatusCode == HttpStatusCode.NotFound)
                return CashierOperationResult<CashierCustomerDetail>.Failure("No encontramos ese cliente.");

            if (!response.IsSuccessStatusCode)
                return CashierOperationResult<CashierCustomerDetail>.Failure("No fue posible buscar el cliente. Intenta de nuevo.");

            var customer = await response.Content.ReadFromJsonAsync<CashierCustomerDetail>(cancellationToken: ct);
            return customer is null
                ? CashierOperationResult<CashierCustomerDetail>.Failure("La respuesta del servidor no fue válida.")
                : CashierOperationResult<CashierCustomerDetail>.Success(customer);
        }
        catch (HttpRequestException)
        {
            return CashierOperationResult<CashierCustomerDetail>.Failure("No hay conexión con LoyaltyCloud. Revisa tu internet e intenta de nuevo.");
        }
        catch (TaskCanceledException)
        {
            return CashierOperationResult<CashierCustomerDetail>.Failure("La conexión tardó demasiado. Intenta de nuevo.");
        }
    }

    public async Task<CashierOperationResult<CashierAddPointsResponse>> AddPointsAsync(
        string serialNumber,
        decimal purchaseAmount,
        CancellationToken ct = default)
    {
        var serial = serialNumber.Trim();
        if (string.IsNullOrWhiteSpace(serial))
            return CashierOperationResult<CashierAddPointsResponse>.Failure("Selecciona un cliente.");

        if (purchaseAmount <= 0m)
            return CashierOperationResult<CashierAddPointsResponse>.Failure("Ingresa un monto mayor a cero.");

        try
        {
            using var response = await _api.PostAsJsonAsync(
                "api/points",
                new CashierAddPointsRequest(serial, purchaseAmount),
                ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return CashierOperationResult<CashierAddPointsResponse>.SessionExpired();

            if (!response.IsSuccessStatusCode)
                return CashierOperationResult<CashierAddPointsResponse>.Failure("No fue posible agregar puntos. Revisa el monto e intenta de nuevo.");

            var result = await response.Content.ReadFromJsonAsync<CashierAddPointsResponse>(cancellationToken: ct);
            return result is null
                ? CashierOperationResult<CashierAddPointsResponse>.Failure("La respuesta del servidor no fue válida.")
                : CashierOperationResult<CashierAddPointsResponse>.Success(result);
        }
        catch (HttpRequestException)
        {
            return CashierOperationResult<CashierAddPointsResponse>.Failure("No hay conexión con LoyaltyCloud. Revisa tu internet e intenta de nuevo.");
        }
        catch (TaskCanceledException)
        {
            return CashierOperationResult<CashierAddPointsResponse>.Failure("La conexión tardó demasiado. Intenta de nuevo.");
        }
    }
}
