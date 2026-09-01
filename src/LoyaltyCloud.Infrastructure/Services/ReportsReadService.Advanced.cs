using LoyaltyCloud.Application.Admin.Queries.AdvancedReports;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed partial class ReportsReadService
{
    public async Task<TopCustomersReportDto> GetTopCustomersAsync(GetTopCustomersReportQuery query, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var points = await _db.PointTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.CreatedAt >= query.StartUtc && t.CreatedAt < query.EndUtc)
            .GroupBy(t => t.LoyaltyCardId)
            .Select(g => new { CardId = g.Key, Activity = g.Count(), Earned = g.Where(t => t.Points > 0 && PointsIssuedTypes.Contains(t.Type)).Sum(t => (int?)t.Points) ?? 0, Amount = g.Where(t => t.Type == TransactionType.Purchase).Sum(t => t.PurchaseAmount ?? 0m) })
            .ToListAsync(ct);
        var redemptions = await _db.Redemptions.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Status != RedemptionStatus.Cancelled && r.RedeemedAt >= query.StartUtc && r.RedeemedAt < query.EndUtc)
            .GroupBy(r => r.LoyaltyCardId)
            .Select(g => new { CardId = g.Key, Count = g.Count(), Points = g.Sum(r => r.PointsSpent) })
            .ToListAsync(ct);
        var cardIds = points.Select(x => x.CardId).Concat(redemptions.Select(x => x.CardId)).Distinct().ToList();
        var customers = await (from card in _db.LoyaltyCards.AsNoTracking()
            join customer in _db.Customers.AsNoTracking() on new { card.TenantId, Id = card.CustomerId } equals new { customer.TenantId, customer.Id }
            where card.TenantId == tenantId && card.IsActive && customer.IsActive && cardIds.Contains(card.Id)
                && (query.Level == null || query.Level == "" || card.Level == query.Level)
            select new { CardId = card.Id, customer.Id, customer.FullName, card.Level }).ToListAsync(ct);
        var pointMap = points.ToDictionary(x => x.CardId);
        var redemptionMap = redemptions.ToDictionary(x => x.CardId);
        decimal Rank(int earned, int redeemed, int activity, decimal amount) => query.Metric switch { TopCustomerMetric.PointsRedeemed => redeemed, TopCustomerMetric.Activity => activity, TopCustomerMetric.PurchaseAmount => amount, _ => earned };
        var rows = customers.Select(c => { pointMap.TryGetValue(c.CardId, out var p); redemptionMap.TryGetValue(c.CardId, out var r); var activity = (p?.Activity ?? 0) + (r?.Count ?? 0); return new TopCustomerRowDto(c.Id, c.FullName, c.Level, activity, p?.Earned ?? 0, r?.Points ?? 0, r?.Count ?? 0, p?.Amount ?? 0m, Rank(p?.Earned ?? 0, r?.Points ?? 0, activity, p?.Amount ?? 0m)); })
            .OrderByDescending(x => x.RankingValue).ThenBy(x => x.CustomerName).Take(Math.Clamp(query.Limit, 1, 200)).ToList().AsReadOnly();
        return new(query.StartUtc, query.EndUtc, query.Metric, rows);
    }

    public async Task<VisitFrequencyReportDto> GetVisitFrequencyAsync(GetVisitFrequencyReportQuery query, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var pointEvents = await (from t in _db.PointTransactions.AsNoTracking() join c in _db.LoyaltyCards.AsNoTracking() on new { t.TenantId, Id = t.LoyaltyCardId } equals new { c.TenantId, c.Id } join customer in _db.Customers.AsNoTracking() on new { c.TenantId, Id = c.CustomerId } equals new { customer.TenantId, customer.Id } where t.TenantId == tenantId && c.IsActive && customer.IsActive && t.CreatedAt >= query.StartUtc && t.CreatedAt < query.EndUtc select new ActivityEvent(c.CustomerId, t.CreatedAt)).ToListAsync(ct);
        var redemptionEvents = await (from r in _db.Redemptions.AsNoTracking() join c in _db.LoyaltyCards.AsNoTracking() on new { r.TenantId, Id = r.LoyaltyCardId } equals new { c.TenantId, c.Id } join customer in _db.Customers.AsNoTracking() on new { c.TenantId, Id = c.CustomerId } equals new { customer.TenantId, customer.Id } where r.TenantId == tenantId && c.IsActive && customer.IsActive && r.Status != RedemptionStatus.Cancelled && r.RedeemedAt >= query.StartUtc && r.RedeemedAt < query.EndUtc select new ActivityEvent(c.CustomerId, r.RedeemedAt)).ToListAsync(ct);
        var visits = pointEvents.Concat(redemptionEvents).GroupBy(x => x.CustomerId).ToDictionary(g => g.Key, g => g.Select(x => x.OccurredAtUtc.Date).Distinct().Order().ToArray());
        var ids = visits.Keys.ToList();
        var customers = await (from c in _db.Customers.AsNoTracking() join card in _db.LoyaltyCards.AsNoTracking() on new { c.TenantId, Id = c.Id } equals new { card.TenantId, Id = card.CustomerId } where c.TenantId == tenantId && c.IsActive && card.IsActive && ids.Contains(c.Id) select new { c.Id, c.FullName, card.Level }).ToListAsync(ct);
        decimal? AverageGap(DateTime[] dates) => dates.Length < 2 ? null : decimal.Round((decimal)dates.Zip(dates.Skip(1), (a, b) => (b - a).TotalDays).Average(), 1);
        var rows = customers.Select(c => new VisitFrequencyRowDto(c.Id, c.FullName, c.Level, visits[c.Id].Length, AverageGap(visits[c.Id]), visits[c.Id][^1]))
            .OrderByDescending(x => x.Visits).ThenBy(x => x.CustomerName).Take(Math.Clamp(query.Limit, 1, 250)).ToList().AsReadOnly();
        var gaps = visits.Values.Select(AverageGap).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return new(query.StartUtc, query.EndUtc, gaps.Count == 0 ? null : decimal.Round(gaps.Average(), 1), visits.Count(x => x.Value.Length == 1), visits.Count(x => x.Value.Length is >= 2 and <= 3), visits.Count(x => x.Value.Length is >= 4 and <= 6), visits.Count(x => x.Value.Length >= 7), rows);
    }

    public async Task<ReturningCustomersReportDto> GetReturningCustomersAsync(GetReturningCustomersReportQuery query, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var firstPointDates = await (from t in _db.PointTransactions.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking() on new { t.TenantId, Id = t.LoyaltyCardId } equals new { card.TenantId, card.Id }
            join customer in _db.Customers.AsNoTracking() on new { card.TenantId, Id = card.CustomerId } equals new { customer.TenantId, customer.Id }
            where t.TenantId == tenantId && card.IsActive && customer.IsActive && t.CreatedAt < query.EndUtc
            group t by card.CustomerId into events
            select new { CustomerId = events.Key, FirstUtc = events.Min(x => x.CreatedAt) }).ToListAsync(ct);
        var firstRedemptionDates = await (from r in _db.Redemptions.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking() on new { r.TenantId, Id = r.LoyaltyCardId } equals new { card.TenantId, card.Id }
            join customer in _db.Customers.AsNoTracking() on new { card.TenantId, Id = card.CustomerId } equals new { customer.TenantId, customer.Id }
            where r.TenantId == tenantId && card.IsActive && customer.IsActive && r.Status != RedemptionStatus.Cancelled && r.RedeemedAt < query.EndUtc
            group r by card.CustomerId into events
            select new { CustomerId = events.Key, FirstUtc = events.Min(x => x.RedeemedAt) }).ToListAsync(ct);
        var firstActivity = firstPointDates.Select(x => (x.CustomerId, x.FirstUtc))
            .Concat(firstRedemptionDates.Select(x => (x.CustomerId, x.FirstUtc)))
            .GroupBy(x => x.CustomerId).ToDictionary(x => x.Key, x => x.Min(y => y.FirstUtc));

        var pointEvents = await (from t in _db.PointTransactions.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking() on new { t.TenantId, Id = t.LoyaltyCardId } equals new { card.TenantId, card.Id }
            join customer in _db.Customers.AsNoTracking() on new { card.TenantId, Id = card.CustomerId } equals new { customer.TenantId, customer.Id }
            where t.TenantId == tenantId && card.IsActive && customer.IsActive && t.CreatedAt >= query.StartUtc && t.CreatedAt < query.EndUtc
            select new ActivityEvent(card.CustomerId, t.CreatedAt)).ToListAsync(ct);
        var redemptionEvents = await (from r in _db.Redemptions.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking() on new { r.TenantId, Id = r.LoyaltyCardId } equals new { card.TenantId, card.Id }
            join customer in _db.Customers.AsNoTracking() on new { card.TenantId, Id = card.CustomerId } equals new { customer.TenantId, customer.Id }
            where r.TenantId == tenantId && card.IsActive && customer.IsActive && r.Status != RedemptionStatus.Cancelled && r.RedeemedAt >= query.StartUtc && r.RedeemedAt < query.EndUtc
            select new ActivityEvent(card.CustomerId, r.RedeemedAt)).ToListAsync(ct);
        var visits = pointEvents.Concat(redemptionEvents).GroupBy(x => x.CustomerId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.OccurredAtUtc.Date).Distinct().ToArray());
        var activeIds = visits.Keys.ToList();
        var newCustomers = activeIds.Count(id => firstActivity.TryGetValue(id, out var first) && first >= query.StartUtc);
        var returningCustomers = activeIds.Count - newCustomers;
        var trend = BuildMonths(query.StartUtc, query.EndUtc).Select(month =>
        {
            var monthEnd = month.AddMonths(1);
            var monthIds = visits.Where(x => x.Value.Any(date => date >= month.Date && date < monthEnd.Date)).Select(x => x.Key).ToList();
            var monthNew = monthIds.Count(id => firstActivity.TryGetValue(id, out var first) && first >= month && first < monthEnd);
            return new CustomerRetentionPeriodDto(month, month.ToString("MMM yy", System.Globalization.CultureInfo.GetCultureInfo("es-MX")), monthNew, monthIds.Count - monthNew);
        }).ToList().AsReadOnly();
        return new(query.StartUtc, query.EndUtc, activeIds.Count, newCustomers, returningCustomers,
            activeIds.Count == 0 ? 0 : decimal.Round(returningCustomers * 100m / activeIds.Count, 1), trend);
    }
    public async Task<ActivityTrendsReportDto> GetActivityTrendsAsync(GetActivityTrendsReportQuery query, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var points = await (from t in _db.PointTransactions.AsNoTracking() join c in _db.LoyaltyCards.AsNoTracking() on new { t.TenantId, Id = t.LoyaltyCardId } equals new { c.TenantId, c.Id } join customer in _db.Customers.AsNoTracking() on new { c.TenantId, Id = c.CustomerId } equals new { customer.TenantId, customer.Id } where t.TenantId == tenantId && c.IsActive && customer.IsActive && t.CreatedAt >= query.StartUtc && t.CreatedAt < query.EndUtc select new { c.CustomerId, t.CreatedAt, t.Points, t.Type, t.PurchaseAmount }).ToListAsync(ct);
        var redemptions = await (from r in _db.Redemptions.AsNoTracking() join c in _db.LoyaltyCards.AsNoTracking() on new { r.TenantId, Id = r.LoyaltyCardId } equals new { c.TenantId, c.Id } join customer in _db.Customers.AsNoTracking() on new { c.TenantId, Id = c.CustomerId } equals new { customer.TenantId, customer.Id } where r.TenantId == tenantId && c.IsActive && customer.IsActive && r.Status != RedemptionStatus.Cancelled && r.RedeemedAt >= query.StartUtc && r.RedeemedAt < query.EndUtc select new { c.CustomerId, r.RedeemedAt, r.PointsSpent }).ToListAsync(ct);
        var periods = BuildMonths(query.StartUtc, query.EndUtc).Select(month => { var end = month.AddMonths(1); var p = points.Where(x => x.CreatedAt >= month && x.CreatedAt < end).ToList(); var r = redemptions.Where(x => x.RedeemedAt >= month && x.RedeemedAt < end).ToList(); return new ActivityTrendPeriodDto(month, month.ToString("MMM yy", System.Globalization.CultureInfo.GetCultureInfo("es-MX")), p.Select(x => x.CustomerId).Concat(r.Select(x => x.CustomerId)).Distinct().Count(), p.Where(x => x.Points > 0 && PointsIssuedTypes.Contains(x.Type)).Sum(x => x.Points), r.Sum(x => x.PointsSpent), r.Count, p.Count(x => x.Type == TransactionType.Purchase), p.Where(x => x.Type == TransactionType.Purchase).Sum(x => x.PurchaseAmount ?? 0m)); }).ToList().AsReadOnly();
        return new(query.StartUtc, query.EndUtc, periods);
    }

    public async Task<LevelDistributionReportDto> GetLevelDistributionAsync(GetLevelDistributionReportQuery query, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var groups = await (from card in _db.LoyaltyCards.AsNoTracking() join customer in _db.Customers.AsNoTracking() on new { card.TenantId, Id = card.CustomerId } equals new { customer.TenantId, customer.Id } where card.TenantId == tenantId && card.IsActive && customer.IsActive select card).GroupBy(c => c.Level).Select(g => new { Level = g.Key, Customers = g.Count(), AveragePoints = g.Average(c => (decimal)c.CurrentPoints) }).OrderByDescending(x => x.Customers).ToListAsync(ct);
        var total = groups.Sum(x => x.Customers);
        var rows = groups.Select(x => new LevelDistributionRowDto(x.Level, x.Customers, total == 0 ? 0 : decimal.Round(x.Customers * 100m / total, 1), decimal.Round(x.AveragePoints, 1))).ToList().AsReadOnly();
        return new(total, rows.FirstOrDefault()?.Level, rows.Count == 0 ? 0 : rows.Max(x => x.Percentage), rows);
    }

    private static IReadOnlyList<DateTime> BuildMonths(DateTime startUtc, DateTime endUtc)
    {
        var start = new DateTime(startUtc.Year, startUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = new List<DateTime>();
        for (var month = start; month < endUtc; month = month.AddMonths(1)) result.Add(month);
        return result;
    }

    private sealed record ActivityEvent(Guid CustomerId, DateTime OccurredAtUtc);
}
