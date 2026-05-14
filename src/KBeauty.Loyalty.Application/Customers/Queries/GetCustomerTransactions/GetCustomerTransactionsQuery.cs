using KBeauty.Loyalty.Common.Pagination;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Customers.Queries.GetCustomerTransactions;

/// <summary>Historial paginado de movimientos de puntos de una clienta.</summary>
public sealed record GetCustomerTransactionsQuery(
    string SerialNumber,
    PaginationParams Pagination) : IRequest<Result<PagedResult<TransactionDto>>>;
