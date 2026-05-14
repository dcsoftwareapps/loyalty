using KBeauty.Loyalty.Application.Customers.Queries.GetCustomers;
using KBeauty.Loyalty.Common.Pagination;
using KBeauty.Loyalty.Common.Results;

namespace KBeauty.Loyalty.Application.Common.Interfaces;

/// <summary>
/// Búsqueda paginada de clientas con filtros — separada de los repositorios
/// porque hace JOIN entre Customer y LoyaltyCard y proyecta a un DTO de lista,
/// no a entidades. La implementación en Infrastructure usa <c>AsNoTracking</c>.
/// </summary>
public interface ICustomerListReadService
{
    Task<PagedResult<CustomerListItemDto>> SearchAsync(
        string? searchTerm,
        string? levelFilter,
        PaginationParams pagination,
        CancellationToken ct = default);
}
