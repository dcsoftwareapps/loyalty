using KBeauty.Loyalty.Application.Admin.Queries.GetAdminDashboard;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Services;

/// <summary>
/// Construye el <see cref="DashboardDto"/> con queries optimizadas en una sola
/// pasada por DbContext. Todo va con <c>AsNoTracking</c> — son lecturas puras.
/// </summary>
internal sealed class DashboardReadService : IDashboardReadService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dt;

    public DashboardReadService(AppDbContext db, IDateTimeProvider dt)
    {
        _db = db;
        _dt = dt;
    }

    /// <inheritdoc />
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var now = _dt.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var activeCustomers = await _db.Customers
            .AsNoTracking()
            .CountAsync(c => c.IsActive, ct);

        var pointsThisMonth = await _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.CreatedAt >= monthStart && t.Points > 0)
            .SumAsync(t => (int?)t.Points, ct) ?? 0;

        var redemptionsThisMonth = await _db.Redemptions
            .AsNoTracking()
            .CountAsync(r => r.RedeemedAt >= monthStart, ct);

        var byLevelList = await _db.LoyaltyCards
            .AsNoTracking()
            .Where(c => c.IsActive)
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
            where t.Type == TransactionType.Purchase
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
}
