using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Repositories;

internal sealed class PointCampaignRepository : IPointCampaignRepository
{
    private readonly AppDbContext _db;

    public PointCampaignRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PointCampaign>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.PointCampaigns
            .AsNoTracking()
            .OrderByDescending(c => c.StartsAtUtc)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public Task<PointCampaign?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.PointCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<PointCampaign?> GetBestApplicableAsync(
        DateTime nowUtc,
        decimal purchaseAmount,
        string loyaltyLevel,
        CancellationToken ct = default)
    {
        var candidates = await _db.PointCampaigns
            .AsNoTracking()
            .Where(c => c.IsActive
                     && c.StartsAtUtc <= nowUtc
                     && c.EndsAtUtc >= nowUtc
                     && (!c.MinimumPurchaseAmount.HasValue || purchaseAmount >= c.MinimumPurchaseAmount.Value))
            .ToListAsync(ct);

        return candidates
            .Where(c => c.AppliesToLevel(loyaltyLevel))
            .OrderByDescending(c => c.Multiplier)
            .ThenBy(c => c.MinimumPurchaseAmount ?? 0)
            .ThenByDescending(c => c.StartsAtUtc)
            .ThenBy(c => c.Id)
            .FirstOrDefault();
    }

    public async Task AddAsync(PointCampaign campaign, CancellationToken ct = default)
    {
        await _db.PointCampaigns.AddAsync(campaign, ct);
    }

    public void Update(PointCampaign campaign)
    {
        if (_db.Entry(campaign).State == EntityState.Detached)
            _db.PointCampaigns.Update(campaign);
    }
}
