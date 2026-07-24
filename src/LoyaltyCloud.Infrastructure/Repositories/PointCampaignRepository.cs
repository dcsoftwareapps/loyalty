using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class PointCampaignRepository : IPointCampaignRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantLoyaltyLevelReadService _tenantLevels;

    public PointCampaignRepository(
        AppDbContext db,
        ITenantContext tenantContext,
        ITenantLoyaltyLevelReadService tenantLevels)
    {
        _db = db;
        _tenantContext = tenantContext;
        _tenantLevels = tenantLevels;
    }

    public async Task<IReadOnlyList<PointCampaign>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.PointCampaigns
            .AsNoTracking()
            .Where(c => c.TenantId == _tenantContext.RequireTenantId())
            .OrderByDescending(c => c.StartsAtUtc)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public Task<PointCampaign?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.PointCampaigns.FirstOrDefaultAsync(c => c.TenantId == _tenantContext.RequireTenantId() && c.Id == id, ct);

    public async Task<PointCampaign?> GetBestApplicableAsync(
        DateTime nowUtc,
        decimal purchaseAmount,
        string loyaltyLevel,
        CancellationToken ct = default)
    {
        var candidates = await _db.PointCampaigns
            .AsNoTracking()
            .Where(c => c.TenantId == _tenantContext.RequireTenantId()
                     && c.IsActive
                     && c.StartsAtUtc <= nowUtc
                     && c.EndsAtUtc >= nowUtc
                     && (!c.MinimumPurchaseAmount.HasValue || purchaseAmount >= c.MinimumPurchaseAmount.Value))
            .ToListAsync(ct);

        var levelRanks = await BuildLevelRanksAsync(ct);

        return candidates
            .Where(c => c.AppliesToLevel(loyaltyLevel, levelRanks))
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

    private async Task<IReadOnlyDictionary<string, int>> BuildLevelRanksAsync(CancellationToken ct)
    {
        var levels = await _tenantLevels.GetActiveLevelsAsync(ct);
        return levels.ToDictionary(
            level => level.Name,
            level => level.SortOrder,
            StringComparer.OrdinalIgnoreCase);
    }
}
