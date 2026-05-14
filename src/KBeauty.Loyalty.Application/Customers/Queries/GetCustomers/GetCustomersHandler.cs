using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Pagination;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Customers.Queries.GetCustomers;

/// <inheritdoc cref="GetCustomersQuery"/>
public sealed class GetCustomersHandler
    : IRequestHandler<GetCustomersQuery, Result<PagedResult<CustomerListItemDto>>>
{
    private readonly ICustomerListReadService _read;

    public GetCustomersHandler(ICustomerListReadService read) => _read = read;

    /// <inheritdoc />
    public async Task<Result<PagedResult<CustomerListItemDto>>> Handle(
        GetCustomersQuery query,
        CancellationToken ct)
    {
        var page = await _read.SearchAsync(query.SearchTerm, query.LevelFilter, query.Pagination, ct);
        return Result.Ok(page);
    }
}
