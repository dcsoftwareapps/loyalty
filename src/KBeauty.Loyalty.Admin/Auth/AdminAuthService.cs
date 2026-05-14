using System.Security.Claims;
using KBeauty.Loyalty.Common.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace KBeauty.Loyalty.Admin.Auth;

/// <summary>
/// Valida credenciales contra <see cref="AdminAuthOptions"/> y firma la cookie
/// de autenticación. Se usa desde la página <c>Login.razor</c> renderizada como
/// Static SSR (única forma de poder setear cookies durante el ciclo de request).
/// </summary>
public sealed class AdminAuthService
{
    private readonly AdminAuthOptions _options;

    public AdminAuthService(IOptions<AdminAuthOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Intenta sign-in. Devuelve true si las credenciales coinciden y la cookie
    /// quedó emitida; false si fallaron.
    /// </summary>
    public async Task<bool> TrySignInAsync(HttpContext context, string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        // Comparación case-sensitive para evitar atajos accidentales.
        if (!string.Equals(username, _options.Username, StringComparison.Ordinal) ||
            !string.Equals(password, _options.Password, StringComparison.Ordinal))
        {
            return false;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.NameIdentifier, username),
            new(ClaimTypes.Role, LoyaltyConstants.Roles.Owner)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(_options.SessionHours)
            });

        return true;
    }

    /// <summary>Borra la cookie y cierra la sesión.</summary>
    public Task SignOutAsync(HttpContext context) =>
        context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}
