using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class DefaultTenantResolutionService : IDefaultTenantResolutionService
{
    private readonly ITenantRepository _tenants;
    private readonly IMutableTenantContext _tenantContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DefaultTenantResolutionService> _logger;

    public DefaultTenantResolutionService(
        ITenantRepository tenants,
        IMutableTenantContext tenantContext,
        IConfiguration configuration,
        ILogger<DefaultTenantResolutionService> logger)
    {
        _tenants = tenants;
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ResolveDefaultTenantIfMissingAsync(CancellationToken ct = default)
    {
        if (_tenantContext.HasTenant)
            return;

        var slug = _configuration["Tenancy:DefaultTenantSlug"];
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidOperationException("Falta Tenancy:DefaultTenantSlug para el fallback temporal MT-2.");

        var tenant = await _tenants.GetBySlugAsync(slug, ct)
            ?? throw new InvalidOperationException($"No existe el tenant fallback temporal '{slug}'.");

        if (!tenant.IsActive)
            throw new InvalidOperationException($"El tenant fallback temporal '{slug}' no esta activo.");

        _tenantContext.SetTenant(tenant.Id, tenant.Slug);
        _logger.LogWarning(
            "MT-2 transitional default tenant fallback resolved TenantId={TenantId}, Slug={TenantSlug}. Remove this in MT-3 when tenant is resolved from route/claims.",
            tenant.Id,
            tenant.Slug);
    }
}
