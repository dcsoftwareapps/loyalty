using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class TenantAdminUserRepository : ITenantAdminUserRepository
{
    private readonly AppDbContext _db;

    public TenantAdminUserRepository(AppDbContext db) => _db = db;

    public Task<TenantAdminUser?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.TenantAdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<TenantAdminUser?> GetByUsernameAsync(Guid tenantId, string username, CancellationToken ct = default)
    {
        var normalizedUsername = TenantAdminUser.NormalizeUsername(username);
        return _db.TenantAdminUsers
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.NormalizedUsername == normalizedUsername, ct);
    }

    public Task<bool> UsernameExistsAsync(Guid tenantId, string username, CancellationToken ct = default)
    {
        var normalizedUsername = TenantAdminUser.NormalizeUsername(username);
        return _db.TenantAdminUsers
            .AnyAsync(u => u.TenantId == tenantId && u.NormalizedUsername == normalizedUsername, ct);
    }

    public async Task AddAsync(TenantAdminUser adminUser, CancellationToken ct = default)
    {
        await _db.TenantAdminUsers.AddAsync(adminUser, ct);
    }

    public void Update(TenantAdminUser adminUser)
    {
        if (_db.Entry(adminUser).State == EntityState.Detached)
            _db.TenantAdminUsers.Update(adminUser);
    }
}
