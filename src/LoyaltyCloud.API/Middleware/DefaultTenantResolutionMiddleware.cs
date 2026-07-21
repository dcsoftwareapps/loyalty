using LoyaltyCloud.Application.Common.Interfaces;

namespace LoyaltyCloud.API.Middleware;

/// <summary>
/// MT-2 transitional fallback. Remove in MT-3 when public routes resolve tenant
/// by slug and Admin resolves tenant from authenticated claims.
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
