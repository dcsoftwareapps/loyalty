using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Redemptions.Queries.ListRedemptions;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class RedemptionHistoryReadService : IRedemptionHistoryReadService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public RedemptionHistoryReadService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<RedemptionHistoryItemDto>> ListAsync(
        RedemptionStatus? status,
        string? search,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var query = from redemption in _db.Redemptions.AsNoTracking()
                    join card in _db.LoyaltyCards.AsNoTracking() on redemption.LoyaltyCardId equals card.Id
                    join customer in _db.Customers.AsNoTracking() on card.CustomerId equals customer.Id
                    join reward in _db.RewardCatalogItems.AsNoTracking() on redemption.RewardCatalogItemId equals reward.Id
                    where redemption.TenantId == tenantId
                       && card.TenantId == tenantId
                       && customer.TenantId == tenantId
                       && reward.TenantId == tenantId
                    select new
                    {
                        redemption,
                        card.SerialNumber,
                        customer.FullName,
                        RewardName = reward.Name
                    };

        if (status.HasValue)
            query = query.Where(x => x.redemption.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.Like(x.FullName, like) ||
                EF.Functions.Like(x.SerialNumber, like) ||
                EF.Functions.Like(x.RewardName, like));
        }

        var items = await query
            .OrderByDescending(x => x.redemption.RedeemedAt)
            .Select(x => new RedemptionHistoryItemDto(
                x.redemption.Id,
                x.FullName,
                x.SerialNumber,
                x.RewardName,
                x.redemption.PointsSpent,
                x.redemption.Status,
                x.redemption.RedeemedAt,
                x.redemption.ConfirmedAt,
                x.redemption.ConfirmedBy,
                x.redemption.Notes))
            .ToListAsync(ct);

        return items.AsReadOnly();
    }
}
