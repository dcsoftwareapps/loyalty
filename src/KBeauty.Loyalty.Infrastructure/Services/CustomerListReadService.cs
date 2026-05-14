using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Customers.Queries.GetCustomers;
using KBeauty.Loyalty.Common.Pagination;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Services;

/// <inheritdoc cref="ICustomerListReadService"/>
internal sealed class CustomerListReadService : ICustomerListReadService
{
    private readonly AppDbContext _db;

    public CustomerListReadService(AppDbContext db) => _db = db;

    public async Task<PagedResult<CustomerListItemDto>> SearchAsync(
        string? searchTerm,
        string? levelFilter,
        PaginationParams pagination,
        CancellationToken ct = default)
    {
        var query = from c in _db.Customers.AsNoTracking()
                    join card in _db.LoyaltyCards.AsNoTracking() on c.Id equals card.CustomerId
                    where c.IsActive
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
