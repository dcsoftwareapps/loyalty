using LoyaltyCloud.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class WalletTenantContextResolver : IWalletTenantContextResolver
{
    private readonly ILoyaltyCardTenantLookup _lookup;
    private readonly IMutableTenantContext _tenantContext;
    private readonly ILogger<WalletTenantContextResolver> _logger;

    public WalletTenantContextResolver(
        ILoyaltyCardTenantLookup lookup,
        IMutableTenantContext tenantContext,
        ILogger<WalletTenantContextResolver> logger)
    {
        _lookup = lookup;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<WalletTenantInfo?> ResolveAndSetTenantAsync(
        string serialNumber,
        bool requireOperational,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _lookup.ResolveBySerialNumberAsync(serialNumber, cancellationToken);
        if (tenant is null)
        {
            _logger.LogWarning(
                "Wallet tenant resolution failed for serial {Serial}: card not found.",
                serialNumber);
            return null;
        }

        if (requireOperational && !tenant.IsOperational)
        {
            _logger.LogWarning(
                "Wallet tenant resolution blocked for serial {Serial}: tenant={TenantSlug}, active={IsActive}, subscription={SubscriptionStatus}.",
                serialNumber,
                tenant.TenantSlug,
                tenant.IsTenantActive,
                tenant.SubscriptionStatus?.ToString() ?? "none");
            return tenant;
        }

        _tenantContext.SetTenant(tenant.TenantId, tenant.TenantSlug);
        _logger.LogInformation(
            "Wallet tenant resolved for serial {Serial}: tenant={TenantSlug} ({TenantId}).",
            serialNumber,
            tenant.TenantSlug,
            tenant.TenantId);

        return tenant;
    }
}

