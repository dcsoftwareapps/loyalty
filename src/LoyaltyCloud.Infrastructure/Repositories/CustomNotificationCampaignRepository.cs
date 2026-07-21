using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class CustomNotificationCampaignRepository : ICustomNotificationCampaignRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CustomNotificationCampaignRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<CustomNotificationCampaign?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.CustomNotificationCampaigns.FirstOrDefaultAsync(c => c.TenantId == _tenantContext.RequireTenantId() && c.Id == id, ct);

    public async Task<IReadOnlyList<CustomNotificationCampaign>> ListAsync(
        CustomNotificationCampaignStatus? status,
        int take,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var query = _db.CustomNotificationCampaigns
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .AsQueryable();
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var rows = await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

        return rows.AsReadOnly();
    }

    public async Task<IReadOnlyList<CustomNotificationCampaign>> GetDueAsync(DateTime nowUtc, int take, CancellationToken ct = default)
    {
        var rows = await _db.CustomNotificationCampaigns
            .Where(c => c.TenantId == _tenantContext.RequireTenantId()
                     && c.Status == CustomNotificationCampaignStatus.Scheduled
                     && c.ScheduledAtUtc.HasValue
                     && c.ScheduledAtUtc <= nowUtc)
            .OrderBy(c => c.ScheduledAtUtc)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);

        return rows.AsReadOnly();
    }

    public async Task AddAsync(CustomNotificationCampaign campaign, CancellationToken ct = default) =>
        await _db.CustomNotificationCampaigns.AddAsync(campaign, ct);

    public void Update(CustomNotificationCampaign campaign)
    {
        if (_db.Entry(campaign).State == EntityState.Detached)
            _db.CustomNotificationCampaigns.Update(campaign);
    }
}
