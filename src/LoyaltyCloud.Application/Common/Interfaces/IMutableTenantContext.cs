namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IMutableTenantContext : ITenantContext
{
    void SetTenant(Guid tenantId, string tenantSlug);
    void Clear();
}
