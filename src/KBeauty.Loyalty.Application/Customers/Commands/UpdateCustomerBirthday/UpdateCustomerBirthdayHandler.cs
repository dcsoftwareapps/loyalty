using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;

namespace KBeauty.Loyalty.Application.Customers.Commands.UpdateCustomerBirthday;

public sealed class UpdateCustomerBirthdayHandler
    : IRequestHandler<UpdateCustomerBirthdayCommand, Result<UpdateCustomerBirthdayResponse>>
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _uow;

    public UpdateCustomerBirthdayHandler(ICustomerRepository customers, IUnitOfWork uow)
    {
        _customers = customers;
        _uow = uow;
    }

    public async Task<Result<UpdateCustomerBirthdayResponse>> Handle(
        UpdateCustomerBirthdayCommand command,
        CancellationToken ct)
    {
        Customer? customer = null;
        if (command.CustomerId.HasValue)
            customer = await _customers.GetByIdAsync(command.CustomerId.Value, ct);
        else if (!string.IsNullOrWhiteSpace(command.SerialNumber))
            customer = await _customers.GetBySerialNumberAsync(command.SerialNumber, ct);

        if (customer is null)
            return Result.Fail<UpdateCustomerBirthdayResponse>("Cliente no encontrado.");

        try
        {
            customer.UpdateBirthday(command.Day, command.Month);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Result.Fail<UpdateCustomerBirthdayResponse>(ex.Message);
        }

        _customers.Update(customer);
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(new UpdateCustomerBirthdayResponse(
            customer.Id,
            customer.FullName,
            customer.DateOfBirth.Day,
            customer.DateOfBirth.Month));
    }
}
