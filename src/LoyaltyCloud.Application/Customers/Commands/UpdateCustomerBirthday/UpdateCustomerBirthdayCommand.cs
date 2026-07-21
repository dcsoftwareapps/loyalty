using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Customers.Commands.UpdateCustomerBirthday;

public sealed record UpdateCustomerBirthdayCommand(
    Guid? CustomerId,
    string? SerialNumber,
    int Day,
    int Month) : IRequest<Result<UpdateCustomerBirthdayResponse>>;

public sealed record UpdateCustomerBirthdayResponse(
    Guid CustomerId,
    string FullName,
    int Day,
    int Month);
