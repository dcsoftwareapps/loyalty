using LoyaltyCloud.Application.Common.Interfaces;

namespace LoyaltyCloud.Admin.Middleware;

/// <summary>
/// MT-2 transitional fallback. Remove in MT-3 when Admin resolves tenant from
/// authenticated claims instead of Tenancy:DefaultTenantSlug.
/// </summary>
public sealed class DefaultTenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public DefaultTenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IDefaultTenantResolutionService resolver)
    {
        if (IsPublicJoinRoute(context.Request.Path))
        {
            await _next(context);
            return;
        }

        await resolver.ResolveDefaultTenantIfMissingAsync(context.RequestAborted);
        await _next(context);
    }

    private static bool IsPublicJoinRoute(PathString path)
    {
        var value = path.Value?.Trim('/');
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 1
            ? string.Equals(segments[0], "join", StringComparison.OrdinalIgnoreCase)
            : segments.Length == 2 && string.Equals(segments[1], "join", StringComparison.OrdinalIgnoreCase);
    }
}
