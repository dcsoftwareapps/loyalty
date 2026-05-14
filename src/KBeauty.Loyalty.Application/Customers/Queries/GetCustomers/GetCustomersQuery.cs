using KBeauty.Loyalty.Common.Pagination;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Customers.Queries.GetCustomers;

/// <summary>Listado paginado para el panel admin con búsqueda y filtro por nivel.</summary>
public sealed record GetCustomersQuery(
    string? SearchTerm,
    string? LevelFilter,
    PaginationParams Pagination) : IRequest<Result<PagedResult<CustomerListItemDto>>>;
