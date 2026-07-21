using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Pagination;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class RedemptionRepository : IRedemptionRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public RedemptionRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<Redemption?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Redemptions.FirstOrDefaultAsync(r => r.TenantId == _tenantContext.RequireTenantId() && r.Id == id, ct);

    public async Task<PagedResult<Redemption>> GetByCardIdAsync(
        Guid loyaltyCardId,
        PaginationParams pagination,
        CancellationToken ct = default)
    {
        var baseQuery = _db.Redemptions
            .AsNoTracking()
            .Where(r => r.TenantId == _tenantContext.RequireTenantId() && r.LoyaltyCardId == loyaltyCardId);

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
            .Where(r => r.TenantId == _tenantContext.RequireTenantId() && r.Status == RedemptionStatus.Pending)
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
