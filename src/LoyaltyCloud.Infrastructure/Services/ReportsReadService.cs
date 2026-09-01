using LoyaltyCloud.Application.Admin.Queries.AdvancedReports;
using LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed partial class ReportsReadService : IReportsReadService
{
    private static readonly TransactionType[] PointsIssuedTypes =
    [
        TransactionType.Purchase,
        TransactionType.BonusWelcome,
        TransactionType.BonusBirthday,
        TransactionType.BonusReferral
    ];

    private static readonly TransactionType[] PointsExpiredTypes =
    [
        TransactionType.Expiry,
        TransactionType.Expired
    ];

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _clock;

    public ReportsReadService(AppDbContext db, ITenantContext tenantContext, IDateTimeProvider clock)
    {
        _db = db;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<ReportsSummaryDto> GetReportsSummaryAsync(
        GetReportsSummaryQuery query,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var periodStartUtc = query.PeriodStartUtc;
        var periodEndUtc = query.PeriodEndUtc;

        var newCustomers = await _db.Customers
            .AsNoTracking()
            .CountAsync(c => c.TenantId == tenantId
                && c.IsActive
                && c.CreatedAt >= periodStartUtc
                && c.CreatedAt < periodEndUtc, ct);

        var pointActiveCustomerIds =
            from transaction in _db.PointTransactions.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking()
                on new { transaction.TenantId, Id = transaction.LoyaltyCardId }
                equals new { card.TenantId, card.Id }
            join customer in _db.Customers.AsNoTracking()
                on new { card.TenantId, Id = card.CustomerId }
                equals new { customer.TenantId, customer.Id }
            where transaction.TenantId == tenantId
                && card.IsActive
                && customer.IsActive
                && transaction.CreatedAt >= periodStartUtc
                && transaction.CreatedAt < periodEndUtc
            select card.CustomerId;

        var redemptionActiveCustomerIds =
            from redemption in _db.Redemptions.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking()
                on new { redemption.TenantId, Id = redemption.LoyaltyCardId }
                equals new { card.TenantId, card.Id }
            join customer in _db.Customers.AsNoTracking()
                on new { card.TenantId, Id = card.CustomerId }
                equals new { customer.TenantId, customer.Id }
            where redemption.TenantId == tenantId
                && card.IsActive
                && customer.IsActive
                && redemption.RedeemedAt >= periodStartUtc
                && redemption.RedeemedAt < periodEndUtc
            select card.CustomerId;

        var activeCustomers = await pointActiveCustomerIds
            .Concat(redemptionActiveCustomerIds)
            .Distinct()
            .CountAsync(ct);

        var purchaseTransactions = _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                && t.Type == TransactionType.Purchase
                && t.CreatedAt >= periodStartUtc
                && t.CreatedAt < periodEndUtc);

        var registeredPurchases = await purchaseTransactions.CountAsync(ct);
        var registeredPurchaseAmount = await purchaseTransactions
            .SumAsync(t => (decimal?)t.PurchaseAmount, ct) ?? 0m;

        var pointsIssued = await _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                && t.CreatedAt >= periodStartUtc
                && t.CreatedAt < periodEndUtc
                && t.Points > 0
                && PointsIssuedTypes.Contains(t.Type))
            .SumAsync(t => (int?)t.Points, ct) ?? 0;

        var countedRedemptions = _db.Redemptions
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.Status != RedemptionStatus.Cancelled
                && r.RedeemedAt >= periodStartUtc
                && r.RedeemedAt < periodEndUtc);

        var redemptions = await countedRedemptions.CountAsync(ct);
        var pointsRedeemed = await countedRedemptions
            .SumAsync(r => (int?)r.PointsSpent, ct) ?? 0;

        var pointsExpired = await _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                && t.CreatedAt >= periodStartUtc
                && t.CreatedAt < periodEndUtc
                && t.Points < 0
                && PointsExpiredTypes.Contains(t.Type))
            .SumAsync(t => (int?)-t.Points, ct) ?? 0;

        var totalCustomers = await _db.Customers
            .AsNoTracking()
            .CountAsync(c => c.TenantId == tenantId && c.IsActive, ct);

        var currentPointBalance = await (
            from card in _db.LoyaltyCards.AsNoTracking()
            join customer in _db.Customers.AsNoTracking()
                on new { card.TenantId, Id = card.CustomerId }
                equals new { customer.TenantId, customer.Id }
            where card.TenantId == tenantId
                && card.IsActive
                && customer.IsActive
            select card)
            .SumAsync(c => (int?)c.CurrentPoints, ct) ?? 0;

        var appleWalletRegistrations = await (
            from registration in _db.DeviceRegistrations.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking()
                on new { registration.TenantId, registration.SerialNumber }
                equals new { card.TenantId, card.SerialNumber }
            join customer in _db.Customers.AsNoTracking()
                on new { card.TenantId, Id = card.CustomerId }
                equals new { customer.TenantId, customer.Id }
            where registration.TenantId == tenantId
                && card.IsActive
                && customer.IsActive
            select registration.Id)
            .CountAsync(ct);

        var googleWalletRecords = await (
            from wallet in _db.MemberDigitalWallets.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking()
                on new { wallet.TenantId, Id = wallet.LoyaltyCardId }
                equals new { card.TenantId, card.Id }
            join customer in _db.Customers.AsNoTracking()
                on new { wallet.TenantId, Id = wallet.CustomerId }
                equals new { customer.TenantId, customer.Id }
            where wallet.TenantId == tenantId
                && wallet.Provider == DigitalWalletProvider.Google
                && card.IsActive
                && customer.IsActive
            select wallet.Id)
            .CountAsync(ct);

        var inactive = await GetInactiveCustomersAsync(
            tenantId,
            query.InactiveDaysThreshold,
            query.InactiveCustomersLimit,
            ct);

        var topRewards = await GetTopRewardsAsync(
            tenantId,
            periodStartUtc,
            periodEndUtc,
            query.TopRewardsLimit,
            ct);

        return new ReportsSummaryDto(
            Period: new ReportsPeriodDto(periodStartUtc, periodEndUtc, query.InactiveDaysThreshold),
            PeriodMetrics: new ReportsPeriodMetricsDto(
                NewCustomers: newCustomers,
                ActiveCustomers: activeCustomers,
                RegisteredPurchases: registeredPurchases,
                RegisteredPurchaseAmount: registeredPurchaseAmount,
                PointsIssued: pointsIssued,
                PointsRedeemed: pointsRedeemed,
                PointsExpired: pointsExpired,
                Redemptions: redemptions),
            CurrentProgram: new ReportsCurrentProgramMetricsDto(
                TotalCustomers: totalCustomers,
                CurrentPointBalance: currentPointBalance,
                AppleWalletRegistrations: appleWalletRegistrations,
                GoogleWalletRecords: googleWalletRecords),
            InactiveCustomers: inactive,
            TopRewards: topRewards);
    }

    public async Task<ReportsInactiveCustomersDto> GetInactiveCustomersAsync(
        GetInactiveCustomersReportQuery query,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        return await GetInactiveCustomersAsync(
            tenantId,
            query.InactiveDaysThreshold,
            query.Limit,
            ct);
    }

    public async Task<IReadOnlyList<ReportsTopRewardDto>> GetTopRewardsAsync(
        GetTopRewardsReportQuery query,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        return await GetTopRewardsAsync(
            tenantId,
            query.PeriodStartUtc,
            query.PeriodEndUtc,
            query.Limit,
            ct);
    }

    private async Task<ReportsInactiveCustomersDto> GetInactiveCustomersAsync(
        Guid tenantId,
        int thresholdDays,
        int limit,
        CancellationToken ct)
    {
        var nowUtc = _clock.UtcNow;
        var cutoffUtc = nowUtc.AddDays(-thresholdDays);

        var cards = await (
            from card in _db.LoyaltyCards.AsNoTracking()
            join customer in _db.Customers.AsNoTracking()
                on new { card.TenantId, Id = card.CustomerId }
                equals new { customer.TenantId, customer.Id }
            where card.TenantId == tenantId && card.IsActive && customer.IsActive
            select new
            {
                card.Id,
                card.CustomerId,
                CustomerName = customer.FullName,
                card.SerialNumber,
                card.Level,
                card.CurrentPoints,
                customer.CreatedAt
            })
            .ToListAsync(ct);

        var lastPointActivity = await _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .GroupBy(t => t.LoyaltyCardId)
            .Select(g => new { LoyaltyCardId = g.Key, LastActivityUtc = g.Max(t => t.CreatedAt) })
            .ToDictionaryAsync(x => x.LoyaltyCardId, x => x.LastActivityUtc, ct);

        var lastRedemptionActivity = await _db.Redemptions
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .GroupBy(r => r.LoyaltyCardId)
            .Select(g => new { LoyaltyCardId = g.Key, LastActivityUtc = g.Max(r => r.RedeemedAt) })
            .ToDictionaryAsync(x => x.LoyaltyCardId, x => x.LastActivityUtc, ct);

        var inactiveCustomers = cards
            .Select(card =>
            {
                var lastActivity = card.CreatedAt;
                if (lastPointActivity.TryGetValue(card.Id, out var pointActivity) && pointActivity > lastActivity)
                    lastActivity = pointActivity;
                if (lastRedemptionActivity.TryGetValue(card.Id, out var redemptionActivity) && redemptionActivity > lastActivity)
                    lastActivity = redemptionActivity;

                return new
                {
                    Card = card,
                    LastActivity = lastActivity,
                    DaysWithoutActivity = Math.Max(0, (int)Math.Floor((nowUtc - lastActivity).TotalDays))
                };
            })
            .Where(row => row.LastActivity < cutoffUtc)
            .OrderByDescending(row => row.Card.CurrentPoints > 0)
            .ThenByDescending(row => row.DaysWithoutActivity)
            .ThenBy(row => row.Card.CustomerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = inactiveCustomers
            .Take(Math.Max(1, limit))
            .Select(row => new ReportsInactiveCustomerDto(
                CustomerId: row.Card.CustomerId,
                CustomerName: row.Card.CustomerName,
                SerialNumber: row.Card.SerialNumber,
                CurrentLevel: row.Card.Level,
                CurrentPoints: row.Card.CurrentPoints,
                LastActivityUtc: row.LastActivity,
                DaysWithoutActivity: row.DaysWithoutActivity))
            .ToList()
            .AsReadOnly();

        return new ReportsInactiveCustomersDto(thresholdDays, inactiveCustomers.Count, items);
    }

    private async Task<IReadOnlyList<ReportsTopRewardDto>> GetTopRewardsAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        int limit,
        CancellationToken ct)
    {
        var rows = await (
            from redemption in _db.Redemptions.AsNoTracking()
            join reward in _db.RewardCatalogItems.AsNoTracking()
                on new { redemption.TenantId, RewardCatalogItemId = redemption.RewardCatalogItemId }
                equals new { reward.TenantId, RewardCatalogItemId = (Guid?)reward.Id }
                into rewards
            from reward in rewards.DefaultIfEmpty()
            where redemption.TenantId == tenantId
                && redemption.Type == RedemptionType.CatalogReward
                && redemption.RewardCatalogItemId != null
                && redemption.Status != RedemptionStatus.Cancelled
                && redemption.RedeemedAt >= periodStartUtc
                && redemption.RedeemedAt < periodEndUtc
            group redemption by reward == null ? "Recompensa no disponible" : reward.Name into g
            orderby g.Count() descending, g.Key
            select new ReportsTopRewardDto(g.Key, g.Count()))
            .Take(Math.Max(1, limit))
            .ToListAsync(ct);

        return rows.AsReadOnly();
    }
}
