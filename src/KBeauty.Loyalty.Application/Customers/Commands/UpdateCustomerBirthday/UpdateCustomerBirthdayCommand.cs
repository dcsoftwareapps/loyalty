using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Customers.Commands.UpdateCustomerBirthday;

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
