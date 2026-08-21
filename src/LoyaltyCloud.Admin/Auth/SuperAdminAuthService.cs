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
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SuperAdminAuthService> _logger;

    public SuperAdminAuthService(
        IPasswordHashingService passwords,
        IOptions<SuperAdminAuthOptions> options,
        IHostEnvironment environment,
        ILogger<SuperAdminAuthService> logger)
    {
        _passwords = passwords;
        _options = options.Value;
        _environment = environment;
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

        await SignInAsync(context, username.Trim());
        return SuperAdminLoginResult.Success;
    }

    public async Task<SuperAdminLoginResult> TryDeveloperSignInAsync(HttpContext context)
    {
        if (!_environment.IsDevelopment() || string.IsNullOrWhiteSpace(_options.Username))
        {
            _logger.LogWarning(
                "Developer platform login rejected. Environment={EnvironmentName}.",
                _environment.EnvironmentName);
            return SuperAdminLoginResult.InvalidCredentials;
        }

        await SignInAsync(context, _options.Username.Trim());
        return SuperAdminLoginResult.Success;
    }

    private async Task SignInAsync(HttpContext context, string username)
    {
        var authTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var claims = new List<Claim>
        {
            new("sub", "platform"),
            new(ClaimTypes.NameIdentifier, "platform"),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, SuperAdminAuthDefaults.Role),
            new("auth_time", authTime)
        };

        var identity = new ClaimsIdentity(
            claims,
            SuperAdminAuthDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(Math.Max(1, _options.SessionHours))
        };

        await context.SignInAsync(
            SuperAdminAuthDefaults.AuthenticationScheme,
            principal,
            properties);

        _logger.LogInformation(
            "Platform admin sign-in diagnostic. Scheme={Scheme}, HasUsernameClaim={HasUsernameClaim}, HasRoleClaim={HasRoleClaim}, IsPersistent={IsPersistent}, ExpiresUtc={ExpiresUtc:O}.",
            SuperAdminAuthDefaults.AuthenticationScheme,
            principal.HasClaim(claim => claim.Type == ClaimTypes.Name),
            principal.HasClaim(claim => claim.Type == ClaimTypes.Role),
            properties.IsPersistent,
            properties.ExpiresUtc);

        _logger.LogInformation("Platform admin logged in.");
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
