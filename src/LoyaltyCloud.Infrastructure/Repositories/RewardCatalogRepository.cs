using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Domain.ValueObjects;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class RewardCatalogRepository : IRewardCatalogRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public RewardCatalogRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<RewardCatalogItem>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var list = await _db.RewardCatalogItems
            .AsNoTracking()
            .Where(r => r.TenantId == _tenantContext.RequireTenantId() && r.IsActive)
            .OrderBy(r => r.PointsCost)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<RewardCatalogItem>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.RewardCatalogItems
            .AsNoTracking()
            .Where(r => r.TenantId == _tenantContext.RequireTenantId())
            .OrderBy(r => r.Name)
            .ThenBy(r => r.PointsCost)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<RewardCatalogItem>> GetByLevelAsync(
        MemberLevel level,
        ProgramConfigSnapshot config,
        CancellationToken ct = default)
    {
        // Trae todos los activos y filtra elegibilidad en memoria
        // (es un set chico — 10-20 ítems — no vale la pena complicar la query).
        var all = await _db.RewardCatalogItems
            .AsNoTracking()
            .Where(r => r.TenantId == _tenantContext.RequireTenantId() && r.IsActive)
            .ToListAsync(ct);

        var eligible = all
            .Where(r => r.IsEligibleFor(level, config))
            .OrderBy(r => r.PointsCost)
            .ToList();

        return eligible.AsReadOnly();
    }

    public Task<RewardCatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.RewardCatalogItems.FirstOrDefaultAsync(r => r.TenantId == _tenantContext.RequireTenantId() && r.Id == id, ct);

    public Task<RewardCatalogItem?> GetCurrentMonthlyProductAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return _db.RewardCatalogItems
            .AsNoTracking()
            .Where(r => r.TenantId == _tenantContext.RequireTenantId()
                     && r.IsActive
                     && r.IsMonthlyProduct
                     && r.ValidFrom <= now
                     && r.ValidTo >= now)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> HasOverlappingActiveMonthlyProductAsync(
        DateTime validFrom,
        DateTime validTo,
        Guid? excludeRewardId = null,
        CancellationToken ct = default)
    {
        var query = _db.RewardCatalogItems
            .AsNoTracking()
            .Where(r => r.TenantId == _tenantContext.RequireTenantId()
                     && r.IsActive
                     && r.IsMonthlyProduct
                     && r.ValidFrom.HasValue
                     && r.ValidTo.HasValue
                     && r.ValidFrom.Value <= validTo
                     && r.ValidTo.Value >= validFrom);

        if (excludeRewardId.HasValue)
            query = query.Where(r => r.Id != excludeRewardId.Value);

        return query.AnyAsync(ct);
    }

    public async Task AddAsync(RewardCatalogItem item, CancellationToken ct = default)
    {
        await _db.RewardCatalogItems.AddAsync(item, ct);
    }

    public void Update(RewardCatalogItem item)
    {
        if (_db.Entry(item).State == EntityState.Detached)
            _db.RewardCatalogItems.Update(item);
    }
}
