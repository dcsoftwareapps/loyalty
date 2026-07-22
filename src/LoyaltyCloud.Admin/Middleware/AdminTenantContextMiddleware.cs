using LoyaltyCloud.Admin.Auth;

namespace LoyaltyCloud.Admin.Middleware;

public sealed class AdminTenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public AdminTenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AdminAuthService auth)
    {
        if (context.Request.Path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var resolved = await auth.TrySetTenantContextFromPrincipalAsync(context);
            if (!resolved)
            {
                context.Response.Redirect(auth.GetLoginPathForCurrentPrincipal(context));
                return;
            }
        }

        await _next(context);
    }
}
