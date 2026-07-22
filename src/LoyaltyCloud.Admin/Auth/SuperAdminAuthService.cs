using System.Globalization;
using System.Security.Claims;
using LoyaltyCloud.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Admin.Auth;

public sealed class SuperAdminAuthService
{
    private readonly IPasswordHashingService _passwords;
    private readonly SuperAdminAuthOptions _options;
    private readonly ILogger<SuperAdminAuthService> _logger;

    public SuperAdminAuthService(
        IPasswordHashingService passwords,
        IOptions<SuperAdminAuthOptions> options,
        ILogger<SuperAdminAuthService> logger)
    {
        _passwords = passwords;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SuperAdminLoginResult> TrySignInAsync(
        HttpContext context,
        string? username,
        string? password)
    {
        if (string.IsNullOrWhiteSpace(_options.Username)
            || string.IsNullOrWhiteSpace(_options.PasswordHash)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password)
            || !string.Equals(username.Trim(), _options.Username.Trim(), StringComparison.Ordinal)
            || !_passwords.VerifyPassword(_options.PasswordHash, password))
        {
            _logger.LogWarning("Platform admin login failed. Reason={Reason}", "invalid_credentials");
            await Task.Delay(TimeSpan.FromMilliseconds(250), context.RequestAborted);
            return SuperAdminLoginResult.InvalidCredentials;
        }

        var authTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var claims = new List<Claim>
        {
            new("sub", "platform"),
            new(ClaimTypes.NameIdentifier, "platform"),
            new(ClaimTypes.Name, username.Trim()),
            new(ClaimTypes.Role, SuperAdminAuthDefaults.Role),
            new("auth_time", authTime)
        };

        var identity = new ClaimsIdentity(
            claims,
            SuperAdminAuthDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(
            SuperAdminAuthDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(Math.Max(1, _options.SessionHours))
            });

        _logger.LogInformation("Platform admin logged in.");
        return SuperAdminLoginResult.Success;
    }

    public async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var role = principal?.FindFirstValue(ClaimTypes.Role);
        var name = principal?.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(name)
            || role != SuperAdminAuthDefaults.Role
            || principal!.HasClaim(c => c.Type is AdminClaimTypes.TenantId or AdminClaimTypes.TenantSlug))
        {
            _logger.LogWarning("Platform principal rejected. Reason={Reason}", "invalid_claims");
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(SuperAdminAuthDefaults.AuthenticationScheme);
        }
    }

    public async Task SignOutAsync(HttpContext context) =>
        await context.SignOutAsync(SuperAdminAuthDefaults.AuthenticationScheme);
}

public enum SuperAdminLoginResult
{
    Success,
    InvalidCredentials
}
