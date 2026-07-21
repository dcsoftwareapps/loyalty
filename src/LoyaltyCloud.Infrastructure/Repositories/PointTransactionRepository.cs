using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Pagination;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class PointTransactionRepository : IPointTransactionRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PointTransactionRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResult<PointTransaction>> GetByCardIdAsync(
        Guid loyaltyCardId,
        PaginationParams pagination,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var baseQuery = _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.LoyaltyCardId == loyaltyCardId);

        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(t => t.CreatedAt)
            .Skip(pagination.Skip)
            .Take(pagination.Take)
            .ToListAsync(ct);

        return PagedResult<PointTransaction>.From(items.AsReadOnly(), total, pagination);
    }

    public Task<int> GetPointsEarnedThisYearAsync(
        Guid loyaltyCardId,
        IDateTimeProvider dt,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var cutoff = dt.UtcNow.AddYears(-1);
        return _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                     && t.LoyaltyCardId == loyaltyCardId
                     && t.CreatedAt >= cutoff
                     && t.Points > 0
                     && LevelProgressTransactionTypes.All.Contains(t.Type))
            .SumAsync(t => (int?)t.Points, ct)
            .ContinueWith(x => x.Result ?? 0, ct);
    }

    public Task<int> GetEligibleLevelPointsAsync(
        Guid loyaltyCardId,
        DateTime windowStartUtc,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        return _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                     && t.LoyaltyCardId == loyaltyCardId
                     && t.CreatedAt >= windowStartUtc
                     && t.Points > 0
                     && LevelProgressTransactionTypes.All.Contains(t.Type))
            .SumAsync(t => (int?)t.Points, ct)
            .ContinueWith(x => x.Result ?? 0, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetEligibleLevelPointsByCardAsync(
        DateTime windowStartUtc,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var rows = await _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                     && t.CreatedAt >= windowStartUtc
                     && t.Points > 0
                     && LevelProgressTransactionTypes.All.Contains(t.Type))
            .GroupBy(t => t.LoyaltyCardId)
            .Select(g => new { LoyaltyCardId = g.Key, Points = g.Sum(t => t.Points) })
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.LoyaltyCardId, x => x.Points);
    }

    public async Task AddAsync(PointTransaction transaction, CancellationToken ct = default)
    {
        await _db.PointTransactions.AddAsync(transaction, ct);
    }
}
