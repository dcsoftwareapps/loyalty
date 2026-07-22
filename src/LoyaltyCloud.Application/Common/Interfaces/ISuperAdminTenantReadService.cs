using LoyaltyCloud.Application.SuperAdmin;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ISuperAdminTenantReadService
{
    Task<IReadOnlyList<PlatformTenantListItemDto>> ListTenantsAsync(CancellationToken cancellationToken = default);
    Task<PlatformTenantDetailDto?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
