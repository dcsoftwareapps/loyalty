using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Customers.Commands.JoinCustomer;

public sealed record JoinCustomerCommand(
    string FirstName,
    string LastName,
    string Phone) : IRequest<Result<JoinCustomerResponse>>;

public sealed record JoinCustomerResponse(
    Guid CustomerId,
    string SerialNumber,
    string FullName,
    string Phone,
    bool AlreadyExists,
    string Message);
