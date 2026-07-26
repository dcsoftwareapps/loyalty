using System.Security.Claims;
using LoyaltyCloud.Application.Common.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;

namespace LoyaltyCloud.Admin.Auth;

/// <summary>
/// Restores the tenant context in Blazor Server circuit scopes from authenticated
/// tenant-admin claims. Platform super admin identities intentionally do not set
/// tenant context.
/// </summary>
public sealed class AdminTenantContextInitializer
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IMutableTenantContext _tenantContext;
    private readonly NavigationManager _navigation;
    private readonly ILogger<AdminTenantContextInitializer> _logger;

    public AdminTenantContextInitializer(
        AuthenticationStateProvider authenticationStateProvider,
        IMutableTenantContext tenantContext,
        NavigationManager navigation,
        ILogger<AdminTenantContextInitializer> logger)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _tenantContext = tenantContext;
        _navigation = navigation;
        _logger = logger;
    }

    public async Task<bool> EnsureTenantContextAsync(CancellationToken ct = default)
    {
        if (IsPlatformRoute())
        {
            if (_tenantContext.HasTenant)
            {
                _tenantContext.Clear();
                _logger.LogDebug("Tenant context cleared for platform route.");
            }

            return false;
        }

        if (_tenantContext.HasTenant)
            return true;

        var principal = (await _authenticationStateProvider.GetAuthenticationStateAsync()).User;
        return TrySetTenantContextFromPrincipal(principal);
    }

    public bool TrySetTenantContextFromPrincipal(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return false;

        var tenantIdRaw = principal.FindFirstValue(AdminClaimTypes.TenantId);
        var tenantSlug = principal.FindFirstValue(AdminClaimTypes.TenantSlug);

        if (!Guid.TryParse(tenantIdRaw, out var tenantId) || string.IsNullOrWhiteSpace(tenantSlug))
        {
            _logger.LogDebug("Authenticated principal has no tenant claims; tenant context remains empty.");
            return false;
        }

        _tenantContext.SetTenant(tenantId, tenantSlug);
        _logger.LogDebug(
            "Admin tenant context restored for circuit. TenantId={TenantId}, TenantSlug={TenantSlug}",
            tenantId,
            tenantSlug);
        return true;
    }

    private bool IsPlatformRoute()
    {
        var relativePath = _navigation.ToBaseRelativePath(_navigation.Uri);
        return relativePath.Equals("platform", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("platform/", StringComparison.OrdinalIgnoreCase);
    }
}
