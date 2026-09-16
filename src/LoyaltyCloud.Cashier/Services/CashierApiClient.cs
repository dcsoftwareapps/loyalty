using System.Net;
using System.Net.Http.Json;

namespace LoyaltyCloud.Cashier.Services;

public sealed class CashierApiClient
{
    private readonly HttpClient _http;

    public CashierApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<CashierLoginResult> LoginAsync(
        string tenantSlug,
        string username,
        string password,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                "api/auth/cashier/login",
                new CashierLoginRequest(tenantSlug.Trim(), username.Trim(), password),
                ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized)
                return CashierLoginResult.Failure("Negocio, usuario o contraseña incorrectos.");

            if (!response.IsSuccessStatusCode)
                return CashierLoginResult.Failure("No fue posible iniciar sesión. Intenta de nuevo.");

            var body = await response.Content.ReadFromJsonAsync<CashierLoginResponse>(cancellationToken: ct);
            if (body is null || string.IsNullOrWhiteSpace(body.AccessToken))
                return CashierLoginResult.Failure("La respuesta del servidor no fue válida.");

            var session = new CashierSession(
                body.AccessToken,
                body.TokenType,
                body.ExpiresAtUtc,
                body.TenantSlug,
                body.UserId,
                body.Username,
                body.Role);

            return CashierLoginResult.Success(session);
        }
        catch (HttpRequestException)
        {
            return CashierLoginResult.Failure("No hay conexión con LoyaltyCloud. Revisa tu internet e intenta de nuevo.");
        }
        catch (TaskCanceledException)
        {
            return CashierLoginResult.Failure("La conexión tardó demasiado. Intenta de nuevo.");
        }
    }
}
