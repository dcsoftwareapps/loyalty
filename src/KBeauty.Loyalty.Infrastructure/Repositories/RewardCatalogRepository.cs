using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Domain.ValueObjects;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Repositories;

internal sealed class RewardCatalogRepository : IRewardCatalogRepository
{
    private readonly AppDbContext _db;

    public RewardCatalogRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<RewardCatalogItem>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var list = await _db.RewardCatalogItems
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.PointsCost)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<RewardCatalogItem>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.RewardCatalogItems
            .AsNoTracking()
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
            .Where(r => r.IsActive)
            .ToListAsync(ct);

        var eligible = all
            .Where(r => r.IsEligibleFor(level, config))
            .OrderBy(r => r.PointsCost)
            .ToList();

        return eligible.AsReadOnly();
    }

    public Task<RewardCatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.RewardCatalogItems.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<RewardCatalogItem?> GetCurrentMonthlyProductAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return _db.RewardCatalogItems
            .AsNoTracking()
            .Where(r => r.IsActive
                     && r.IsMonthlyProduct
                     && (r.ValidFrom == null || r.ValidFrom <= now)
                     && (r.ValidTo == null || r.ValidTo >= now))
            .FirstOrDefaultAsync(ct);
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
