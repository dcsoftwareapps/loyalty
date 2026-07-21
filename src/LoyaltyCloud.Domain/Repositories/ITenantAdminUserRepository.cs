using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Domain.Repositories;

public interface ITenantAdminUserRepository
{
    Task<TenantAdminUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantAdminUser?> GetByUsernameAsync(Guid tenantId, string username, CancellationToken ct = default);
    Task<bool> UsernameExistsAsync(Guid tenantId, string username, CancellationToken ct = default);
    Task AddAsync(TenantAdminUser adminUser, CancellationToken ct = default);
    void Update(TenantAdminUser adminUser);
}
