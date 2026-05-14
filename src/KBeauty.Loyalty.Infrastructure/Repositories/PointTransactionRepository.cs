using KBeauty.Loyalty.Common.Pagination;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Repositories;

internal sealed class PointTransactionRepository : IPointTransactionRepository
{
    private readonly AppDbContext _db;

    public PointTransactionRepository(AppDbContext db) => _db = db;

    public async Task<PagedResult<PointTransaction>> GetByCardIdAsync(
        Guid loyaltyCardId,
        PaginationParams pagination,
        CancellationToken ct = default)
    {
        // Lectura pura — sin tracking.
        var baseQuery = _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.LoyaltyCardId == loyaltyCardId);

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
        // Suma de puntos positivos (Purchase / BonusXxx) en los últimos 12 meses.
        var cutoff = dt.UtcNow.AddYears(-1);
        return _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.LoyaltyCardId == loyaltyCardId
                     && t.CreatedAt >= cutoff
                     && t.Points > 0)
            .SumAsync(t => (int?)t.Points, ct)
            .ContinueWith(x => x.Result ?? 0, ct);
    }

    public async Task AddAsync(PointTransaction transaction, CancellationToken ct = default)
    {
        await _db.PointTransactions.AddAsync(transaction, ct);
    }
}
