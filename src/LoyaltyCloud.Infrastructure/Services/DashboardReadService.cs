using LoyaltyCloud.Application.Admin.Queries.GetAdminDashboard;
using LoyaltyCloud.Application.Admin.Queries.GetDashboardSummary;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

/// <summary>
/// Construye el <see cref="DashboardDto"/> con queries optimizadas en una sola
/// pasada por DbContext. Todo va con <c>AsNoTracking</c> — son lecturas puras.
/// </summary>
internal sealed class DashboardReadService : IDashboardReadService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dt;
    private readonly ITenantContext _tenantContext;

    public DashboardReadService(AppDbContext db, IDateTimeProvider dt, ITenantContext tenantContext)
    {
        _db = db;
        _dt = dt;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var now = _dt.UtcNow;
        var tenantId = _tenantContext.RequireTenantId();
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var activeCustomers = await _db.Customers
            .AsNoTracking()
            .CountAsync(c => c.TenantId == tenantId && c.IsActive, ct);

        var pointsThisMonth = await _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.CreatedAt >= monthStart && t.Points > 0)
            .SumAsync(t => (int?)t.Points, ct) ?? 0;

        var redemptionsThisMonth = await _db.Redemptions
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId && r.RedeemedAt >= monthStart, ct);

        var byLevelList = await _db.LoyaltyCards
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .GroupBy(c => c.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byLevel = byLevelList.ToDictionary(x => x.Level, x => x.Count);

        // Últimas 10 visitas (Purchase). JOIN explícito con LoyaltyCards y Customers
        // para devolver datos legibles en una sola query.
        var recentVisits = await (
            from t in _db.PointTransactions.AsNoTracking()
            join c in _db.LoyaltyCards.AsNoTracking() on t.LoyaltyCardId equals c.Id
            join cust in _db.Customers.AsNoTracking() on c.CustomerId equals cust.Id
            where c.TenantId == tenantId && cust.TenantId == tenantId && t.Type == TransactionType.Purchase
            orderby t.CreatedAt descending
            select new RecentVisitDto(
                t.Id,
                cust.FullName,
                c.SerialNumber,
                c.Level,
                t.Points,
                t.PurchaseAmount,
                t.CreatedAt))
            .Take(10)
            .ToListAsync(ct);

        return new DashboardDto(
            ActiveCustomersCount: activeCustomers,
            PointsIssuedThisMonth: pointsThisMonth,
            RedemptionsThisMonth: redemptionsThisMonth,
            CustomersByLevel: byLevel,
            RecentVisits: recentVisits.AsReadOnly());
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        var now = _dt.UtcNow;
        var tenantId = _tenantContext.RequireTenantId();
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalCustomers = await _db.Customers
            .AsNoTracking()
            .CountAsync(c => c.TenantId == tenantId, ct);

        var newCustomersThisMonth = await _db.Customers
            .AsNoTracking()
            .CountAsync(c => c.TenantId == tenantId && c.CreatedAt >= monthStart, ct);

        var customersWithWallet = await _db.LoyaltyCards
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.CustomerId)
            .Distinct()
            .CountAsync(ct);

        var customersWithPointActivity = _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .Select(t => t.LoyaltyCardId);

        var customersWithRedemptions = _db.Redemptions
            .AsNoTracking()
            .Select(r => r.LoyaltyCardId);

        var activeCustomerIds = await _db.LoyaltyCards
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && (
                customersWithPointActivity.Contains(c.Id) ||
                customersWithRedemptions.Contains(c.Id)))
            .Select(c => c.CustomerId)
            .Distinct()
            .CountAsync(ct);

        var pointsIssued = await _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Points > 0)
            .SumAsync(t => (int?)t.Points, ct) ?? 0;

        var pointsRedeemed = await _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Type == TransactionType.Redemption && t.Points < 0)
            .SumAsync(t => (int?)-t.Points, ct) ?? 0;

        var pointsExpired = await _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Type == TransactionType.Expired && t.Points < 0)
            .SumAsync(t => (int?)-t.Points, ct) ?? 0;

        var currentPointBalance = await _db.LoyaltyCards
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .SumAsync(c => (int?)c.CurrentPoints, ct) ?? 0;

        var pendingRedemptions = await _db.Redemptions
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId && r.Status == RedemptionStatus.Pending, ct);

        var confirmedRedemptions = await _db.Redemptions
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId && r.Status == RedemptionStatus.Confirmed, ct);

        var cancelledRedemptions = await _db.Redemptions
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId && r.Status == RedemptionStatus.Cancelled, ct);

        var totalRedemptions = pendingRedemptions + confirmedRedemptions + cancelledRedemptions;

        var totalRewards = await _db.RewardCatalogItems
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId, ct);

        var activeRewards = await _db.RewardCatalogItems
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId && r.IsActive, ct);

        var recentRedemptions = await (
            from redemption in _db.Redemptions.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking() on redemption.LoyaltyCardId equals card.Id
            join customer in _db.Customers.AsNoTracking() on card.CustomerId equals customer.Id
            join reward in _db.RewardCatalogItems.AsNoTracking() on redemption.RewardCatalogItemId equals reward.Id
            where redemption.TenantId == tenantId
               && card.TenantId == tenantId
               && customer.TenantId == tenantId
               && reward.TenantId == tenantId
            orderby redemption.RedeemedAt descending
            select new DashboardRecentActivityItemDto(
                "Canje",
                reward.Name,
                customer.FullName,
                card.SerialNumber,
                -redemption.PointsSpent,
                redemption.Status.ToString(),
                redemption.RedeemedAt))
            .Take(10)
            .ToListAsync(ct);

        var recentPointTransactions = await (
            from transaction in _db.PointTransactions.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking() on transaction.LoyaltyCardId equals card.Id
            join customer in _db.Customers.AsNoTracking() on card.CustomerId equals customer.Id
            where transaction.TenantId == tenantId && card.TenantId == tenantId && customer.TenantId == tenantId
            orderby transaction.CreatedAt descending
            select new DashboardRecentActivityItemDto(
                "Puntos",
                transaction.Description,
                customer.FullName,
                card.SerialNumber,
                transaction.Points,
                transaction.Type.ToString(),
                transaction.CreatedAt))
            .Take(10)
            .ToListAsync(ct);

        var recentActivity = recentRedemptions
            .Concat(recentPointTransactions)
            .OrderByDescending(a => a.OccurredAt)
            .Take(10)
            .ToList()
            .AsReadOnly();

        return new DashboardSummaryDto(
            Customers: new DashboardCustomerMetricsDto(
                TotalCustomers: totalCustomers,
                NewCustomersThisMonth: newCustomersThisMonth,
                CustomersWithWallet: customersWithWallet,
                ActiveCustomers: activeCustomerIds),
            Points: new DashboardPointMetricsDto(
                PointsIssued: pointsIssued,
                PointsRedeemed: pointsRedeemed,
                PointsExpired: pointsExpired,
                CurrentPointBalance: currentPointBalance),
            Redemptions: new DashboardRedemptionMetricsDto(
                Pending: pendingRedemptions,
                Confirmed: confirmedRedemptions,
                Cancelled: cancelledRedemptions,
                Total: totalRedemptions),
            Rewards: new DashboardRewardMetricsDto(
                Total: totalRewards,
                Active: activeRewards,
                Inactive: totalRewards - activeRewards),
            RecentActivity: recentActivity);
    }
}
