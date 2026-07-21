using LoyaltyCloud.Common.Pagination;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Customers.Queries.GetCustomerTransactions;

/// <summary>Historial paginado de movimientos de puntos de una clienta.</summary>
public sealed record GetCustomerTransactionsQuery(
    string SerialNumber,
    PaginationParams Pagination) : IRequest<Result<PagedResult<TransactionDto>>>;
