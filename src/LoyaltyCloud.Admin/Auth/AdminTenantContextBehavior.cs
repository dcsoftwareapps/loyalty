using System.Security.Claims;
using LoyaltyCloud.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;

namespace LoyaltyCloud.Admin.Auth;

/// <summary>
/// Restores the tenant context inside Blazor Server circuit scopes before
/// Admin-triggered MediatR requests reach tenant-owned Application handlers.
/// </summary>
public sealed class AdminTenantContextBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IMutableTenantContext _tenantContext;
    private readonly ILogger<AdminTenantContextBehavior<TRequest, TResponse>> _logger;

    public AdminTenantContextBehavior(
        AuthenticationStateProvider authenticationStateProvider,
        IMutableTenantContext tenantContext,
        ILogger<AdminTenantContextBehavior<TRequest, TResponse>> logger)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
            await TrySetTenantContextFromAuthenticatedPrincipalAsync();

        return await next();
    }

    private async Task TrySetTenantContextFromAuthenticatedPrincipalAsync()
    {
        var principal = (await _authenticationStateProvider.GetAuthenticationStateAsync()).User;
        if (principal.Identity?.IsAuthenticated != true)
            return;

        var tenantIdRaw = principal.FindFirstValue(AdminClaimTypes.TenantId);
        var tenantSlug = principal.FindFirstValue(AdminClaimTypes.TenantSlug);

        if (!Guid.TryParse(tenantIdRaw, out var tenantId) || string.IsNullOrWhiteSpace(tenantSlug))
            return;

        _tenantContext.SetTenant(tenantId, tenantSlug);
        _logger.LogDebug(
            "Admin tenant context restored for circuit. TenantId={TenantId}, TenantSlug={TenantSlug}",
            tenantId,
            tenantSlug);
    }
}
