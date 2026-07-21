using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Customers.Queries.GetCustomers;
using LoyaltyCloud.Common.Pagination;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

/// <inheritdoc cref="ICustomerListReadService"/>
internal sealed class CustomerListReadService : ICustomerListReadService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CustomerListReadService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResult<CustomerListItemDto>> SearchAsync(
        string? searchTerm,
        string? levelFilter,
        PaginationParams pagination,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var query = from c in _db.Customers.AsNoTracking()
                    join card in _db.LoyaltyCards.AsNoTracking() on c.Id equals card.CustomerId
                    where c.TenantId == tenantId && card.TenantId == tenantId && c.IsActive
                    select new { c, card };

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var like = $"%{searchTerm.Trim()}%";
            query = query.Where(x =>
                EF.Functions.Like(x.c.FullName, like) ||
                EF.Functions.Like(x.c.Email, like) ||
                EF.Functions.Like(x.card.SerialNumber, like));
        }

        if (!string.IsNullOrWhiteSpace(levelFilter))
        {
            var level = levelFilter.Trim();
            query = query.Where(x => x.card.Level == level);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.c.CreatedAt)
            .Skip(pagination.Skip)
            .Take(pagination.Take)
            .Select(x => new CustomerListItemDto(
                x.c.Id,
                x.c.FullName,
                x.c.Email,
                x.card.SerialNumber,
                x.card.Level,
                x.card.CurrentPoints,
                x.c.CreatedAt))
            .ToListAsync(ct);

        return PagedResult<CustomerListItemDto>.From(items.AsReadOnly(), total, pagination);
    }
}
