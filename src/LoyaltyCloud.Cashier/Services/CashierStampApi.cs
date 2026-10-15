using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyCloud.Common.Services;

namespace LoyaltyCloud.Cashier.Services;

public sealed class CashierStampApi(AuthenticatedCashierApiClient api) : IStampCashierApi
{
    public Task<StampCall<CashierStampState>> ReadAsync(string serial) => SendAsync<CashierStampState>(
        () => api.GetAsync($"api/stamps/{Uri.EscapeDataString(serial)}"));

    public Task<StampCall<CashierStampResult>> ExecuteAsync(PendingStampOperation operation)
    {
        var action = operation.Operation switch
        {
            "Earn" => "earn",
            "Reversal" => "reverse-last",
            "Redeem" => $"rewards/{operation.RewardId}/redeem",
            _ => throw new InvalidOperationException("Operación desconocida.")
        };
        return SendAsync<CashierStampResult>(() => api.PostAsJsonAsync(
            $"api/stamps/{Uri.EscapeDataString(operation.SerialNumber)}/{action}", new { operation.IdempotencyKey }));
    }

    private static async Task<StampCall<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send) where T : class
    {
        try
        {
            using var response = await send();
            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<T>();
                return new(value, value is null ? "Respuesta no válida." : null, value is null);
            }
            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout
                || response.StatusCode == HttpStatusCode.TooManyRequests)
                return new(null, "No se pudo confirmar la operación.", true);
            // Authentication failure cannot establish whether a previous attempt committed.
            if (response.StatusCode == HttpStatusCode.Unauthorized) return new(null, "Inicia sesión de nuevo para resolver la operación.", true);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return new(null, body.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() : "Operación rechazada.");
        }
        catch (HttpRequestException) { return new(null, "Revisa tu conexión.", true); }
        catch (TaskCanceledException) { return new(null, "La conexión tardó demasiado.", true); }
        catch (JsonException) { return new(null, "Respuesta no válida.", true); }
    }
}

