using LoyaltyCloud.Common.Pagination;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Customers.Queries.GetCustomers;

/// <summary>Listado paginado para el panel admin con búsqueda y filtro por nivel.</summary>
public sealed record GetCustomersQuery(
    string? SearchTerm,
    string? LevelFilter,
    PaginationParams Pagination) : IRequest<Result<PagedResult<CustomerListItemDto>>>;
