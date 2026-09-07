using System.Security.Claims;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.API.Auth;

public sealed class CashierTenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CashierTenantContextMiddleware> _logger;

    public CashierTenantContextMiddleware(
        RequestDelegate next,
        ILogger<CashierTenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        AppDbContext db,
        IMutableTenantContext tenantContext,
        LoyaltyCloud.Common.Services.IDateTimeProvider clock)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && string.Equals(context.User.Identity.AuthenticationType, CashierAuthDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            if (!await TrySetTenantContextAsync(context, db, tenantContext, clock, context.RequestAborted))
                return;
        }

        await _next(context);
    }

    private async Task<bool> TrySetTenantContextAsync(
        HttpContext context,
        AppDbContext db,
        IMutableTenantContext tenantContext,
        LoyaltyCloud.Common.Services.IDateTimeProvider clock,
        CancellationToken ct)
    {
        var tenantIdRaw = context.User.FindFirstValue(CashierClaimTypes.TenantId);
        var tenantSlug = context.User.FindFirstValue(CashierClaimTypes.TenantSlug);
        var userIdRaw = context.User.FindFirstValue(CashierClaimTypes.Subject);
        var roleRaw = context.User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(tenantIdRaw, out var tenantId)
            || !Guid.TryParse(userIdRaw, out var userId)
            || string.IsNullOrWhiteSpace(tenantSlug)
            || !Enum.TryParse<TenantUserRole>(roleRaw, out var tokenRole))
        {
            Reject(context, "invalid_claims");
            return false;
        }

        var tenant = await db.Tenants
            .AsNoTracking()
            .Include(t => t.Subscription)
            .SingleOrDefaultAsync(t => t.Id == tenantId && t.Slug == tenantSlug, ct);
        if (tenant is null || !tenant.IsActive || tenant.Subscription is null || !tenant.Subscription.IsOperational(clock.UtcNow))
        {
            Reject(context, "tenant_unavailable");
            return false;
        }

        tenantContext.SetTenant(tenant.Id, tenant.Slug);
        var user = await db.TenantAdminUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId && u.TenantId == tenant.Id, ct);
        if (user is null || !user.IsActive || user.Role != tokenRole)
        {
            Reject(context, "user_inactive_or_role_changed");
            return false;
        }

        context.Request.Headers[CashierAuthDefaults.OperatorIdHeader] = user.Id.ToString();
        return true;
    }

    private void Reject(HttpContext context, string reason)
    {
        _logger.LogWarning(
            "Cashier bearer request rejected. path={Path}, reason={Reason}.",
            context.Request.Path,
            reason);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }
}
