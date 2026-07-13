using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Customers.Queries.GetCustomerDetail;

public sealed record GetCustomerDetailQuery(Guid CustomerId) : IRequest<Result<CustomerDetailDto>>;
