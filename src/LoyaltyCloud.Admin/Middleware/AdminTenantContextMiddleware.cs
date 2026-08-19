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

            if (await auth.IsBillingOnlyAsync(context)
                && !IsBillingPath(context.Request.Path)
                && !IsBillingInfrastructurePath(context.Request.Path))
            {
                var slug = context.User.FindFirst(LoyaltyCloud.Admin.Auth.AdminClaimTypes.TenantSlug)?.Value;
                context.Response.Redirect($"/{slug}/billing");
                return;
            }
        }

        await _next(context);
    }

    public static bool IsBillingInfrastructurePath(PathString path) =>
        path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase);

    public static bool IsBillingPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return segments.Length == 2 && string.Equals(segments[1], "billing", StringComparison.OrdinalIgnoreCase) ||
            segments.Length == 4 &&
            string.Equals(segments[1], "billing", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[2], "payment", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(segments[3], "success", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(segments[3], "cancelled", StringComparison.OrdinalIgnoreCase)) ||
            path.StartsWithSegments("/logout", StringComparison.OrdinalIgnoreCase);
    }
}
