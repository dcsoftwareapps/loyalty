using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class TenantRepository : ITenantRepository
{
    private readonly AppDbContext _db;

    public TenantRepository(AppDbContext db) => _db = db;

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Tenants
            .Include(t => t.Branding)
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalizedSlug = Tenant.NormalizeSlug(slug);
        return _db.Tenants
            .Include(t => t.Branding)
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync(t => t.Slug == normalizedSlug, ct);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
    {
        var normalizedSlug = Tenant.NormalizeSlug(slug);
        return _db.Tenants.AnyAsync(t => t.Slug == normalizedSlug, ct);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        await _db.Tenants.AddAsync(tenant, ct);
    }

    public void Update(Tenant tenant)
    {
        if (_db.Entry(tenant).State == EntityState.Detached)
            _db.Tenants.Update(tenant);
    }
}
