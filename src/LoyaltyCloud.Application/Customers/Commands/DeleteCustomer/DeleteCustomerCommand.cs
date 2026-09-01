using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Customers.Commands.DeleteCustomer;

public sealed record DeleteCustomerCommand(Guid CustomerId) : IRequest<Result>;
