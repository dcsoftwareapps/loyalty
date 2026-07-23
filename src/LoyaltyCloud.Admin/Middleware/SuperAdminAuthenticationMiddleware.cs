using LoyaltyCloud.Admin.Auth;
using Microsoft.AspNetCore.Authentication;

namespace LoyaltyCloud.Admin.Middleware;

public sealed class SuperAdminAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public SuperAdminAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/platform/login", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase))
        {
            var result = await context.AuthenticateAsync(SuperAdminAuthDefaults.AuthenticationScheme);
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
