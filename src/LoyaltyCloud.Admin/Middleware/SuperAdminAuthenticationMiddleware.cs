using LoyaltyCloud.Admin.Auth;
using Microsoft.AspNetCore.Authentication;

namespace LoyaltyCloud.Admin.Middleware;

public sealed class SuperAdminAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SuperAdminAuthenticationMiddleware> _logger;

    public SuperAdminAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<SuperAdminAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/platform/login", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.Equals("/platform/developer-login", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase))
        {
            var result = await context.AuthenticateAsync(SuperAdminAuthDefaults.AuthenticationScheme);
            var principal = result.Principal;
            var identity = principal?.Identity;
            var claimTypes = principal?.Claims
                .Select(claim => claim.Type)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(type => type, StringComparer.Ordinal)
                .ToArray() ?? [];
            var isInRole = principal?.IsInRole(SuperAdminAuthDefaults.Role) == true;
            var hasPlatformCookie = context.Request.Cookies.ContainsKey("loyaltycloud.platform.auth");

            _logger.LogInformation(
                "Platform auth diagnostic. Path={Path}, Scheme={Scheme}, HasPlatformCookie={HasPlatformCookie}, Succeeded={Succeeded}, None={None}, FailureType={FailureType}, FailureMessage={FailureMessage}, PrincipalNull={PrincipalNull}, IdentityAuthenticated={IdentityAuthenticated}, AuthenticationType={AuthenticationType}, ClaimCount={ClaimCount}, ClaimTypes=[{ClaimTypes}], IsInRole={IsInRole}.",
                context.Request.Path.Value,
                SuperAdminAuthDefaults.AuthenticationScheme,
                hasPlatformCookie,
                result.Succeeded,
                result.None,
                result.Failure?.GetType().Name ?? "null",
                result.Failure?.Message ?? "null",
                principal is null,
                identity?.IsAuthenticated,
                identity?.AuthenticationType ?? "null",
                principal?.Claims.Count() ?? 0,
                string.Join(",", claimTypes),
                isInRole);

            if (result.Succeeded && !isInRole)
            {
                _logger.LogWarning(
                    "Platform authentication succeeded but required role claim is missing. Path={Path}, Scheme={Scheme}, ClaimTypes=[{ClaimTypes}].",
                    context.Request.Path.Value,
                    SuperAdminAuthDefaults.AuthenticationScheme,
                    string.Join(",", claimTypes));
            }

            if (!result.Succeeded || result.Principal is null || !result.Principal.IsInRole(SuperAdminAuthDefaults.Role))
            {
                await context.ChallengeAsync(SuperAdminAuthDefaults.AuthenticationScheme);
                return;
            }

            context.User = result.Principal;
        }

        await _next(context);
    }
}
