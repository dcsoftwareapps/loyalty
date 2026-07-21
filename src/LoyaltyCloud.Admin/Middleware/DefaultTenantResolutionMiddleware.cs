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
        await resolver.ResolveDefaultTenantIfMissingAsync(context.RequestAborted);
        await _next(context);
    }
}
