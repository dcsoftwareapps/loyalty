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
        CancellationToken ct = default, bool rememberMe = false)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                "api/auth/cashier/login",
                new CashierLoginRequest(tenantSlug.Trim(), username.Trim(), password, rememberMe),
                ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized)
                return CashierLoginResult.Failure("Negocio, usuario o contraseña incorrectos.");

            if (!response.IsSuccessStatusCode)
                return CashierLoginResult.Failure("No fue posible iniciar sesión. Intenta de nuevo.");

            var body = await response.Content.ReadFromJsonAsync<CashierLoginResponse>(cancellationToken: ct);
            if (body is null || string.IsNullOrWhiteSpace(body.AccessToken) || body.ProgramType is not ("Points" or "Stamps"))
                return CashierLoginResult.Failure("La respuesta del servidor no fue válida.");

            var session = new CashierSession(
                body.AccessToken,
                body.TokenType,
                body.ExpiresAtUtc,
                body.TenantSlug,
                body.UserId,
                body.Username,
                body.Role,
                body.ProgramType, body.RefreshToken, body.RefreshTokenExpiresAtUtc);

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
    public async Task<CashierSession?> RefreshAsync(string refreshToken,CancellationToken ct=default)
    {
        using var response=await _http.PostAsJsonAsync("api/auth/cashier/refresh",new { refreshToken },ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest) return null;
        response.EnsureSuccessStatusCode();
        var body=await response.Content.ReadFromJsonAsync<CashierLoginResponse>(cancellationToken:ct);
        if (body is null || string.IsNullOrWhiteSpace(body.AccessToken) || string.IsNullOrWhiteSpace(body.RefreshToken)
            || body.ProgramType is not ("Points" or "Stamps") || body.ExpiresAtUtc <= DateTimeOffset.UtcNow
            || body.RefreshTokenExpiresAtUtc is null || body.RefreshTokenExpiresAtUtc <= DateTimeOffset.UtcNow) throw new HttpRequestException("Invalid refresh response.");
        return new(body.AccessToken,body.TokenType,body.ExpiresAtUtc,body.TenantSlug,body.UserId,body.Username,body.Role,
            body.ProgramType,body.RefreshToken,body.RefreshTokenExpiresAtUtc);
    }
    public async Task RevokeAsync(string refreshToken,CancellationToken ct=default)
    {
        using var response=await _http.PostAsJsonAsync("api/auth/cashier/logout",new { refreshToken },ct);
        response.EnsureSuccessStatusCode();
    }

}
