using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Customers.Queries.GetCustomerDetail;

public sealed record GetCustomerDetailQuery(Guid CustomerId) : IRequest<Result<CustomerDetailDto>>;
