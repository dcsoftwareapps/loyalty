using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Customers.Queries.GetCustomerDetail;

public sealed class GetCustomerDetailHandler : IRequestHandler<GetCustomerDetailQuery, Result<CustomerDetailDto>>
{
    private readonly ICustomerDetailReadService _read;

    public GetCustomerDetailHandler(ICustomerDetailReadService read) => _read = read;

    public async Task<Result<CustomerDetailDto>> Handle(GetCustomerDetailQuery query, CancellationToken ct)
    {
        var detail = await _read.GetByCustomerIdAsync(query.CustomerId, ct);
        if (detail is null)
            return Result.Fail<CustomerDetailDto>($"No se encontro cliente con id '{query.CustomerId}'.");

        return Result.Ok(detail);
    }
}
