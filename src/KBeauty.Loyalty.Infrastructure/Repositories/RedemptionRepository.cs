using KBeauty.Loyalty.Common.Pagination;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Repositories;

internal sealed class RedemptionRepository : IRedemptionRepository
{
    private readonly AppDbContext _db;

    public RedemptionRepository(AppDbContext db) => _db = db;

    public Task<Redemption?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Redemptions.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<PagedResult<Redemption>> GetByCardIdAsync(
        Guid loyaltyCardId,
        PaginationParams pagination,
        CancellationToken ct = default)
    {
        var baseQuery = _db.Redemptions
            .AsNoTracking()
            .Where(r => r.LoyaltyCardId == loyaltyCardId);

        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(r => r.RedeemedAt)
            .Skip(pagination.Skip)
            .Take(pagination.Take)
            .ToListAsync(ct);

        return PagedResult<Redemption>.From(items.AsReadOnly(), total, pagination);
    }

    public async Task<IReadOnlyList<Redemption>> GetPendingAsync(CancellationToken ct = default)
    {
        var list = await _db.Redemptions
            .AsNoTracking()
            .Where(r => r.Status == RedemptionStatus.Pending)
            .OrderByDescending(r => r.RedeemedAt)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task AddAsync(Redemption redemption, CancellationToken ct = default)
    {
        await _db.Redemptions.AddAsync(redemption, ct);
    }

    public void Update(Redemption redemption)
    {
        if (_db.Entry(redemption).State == EntityState.Detached)
            _db.Redemptions.Update(redemption);
    }
}
