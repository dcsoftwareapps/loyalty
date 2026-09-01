using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Customers.Commands.DeleteCustomer;

public sealed class DeleteCustomerHandler : IRequestHandler<DeleteCustomerCommand, Result>
{
    private readonly ICustomerRepository _customers;
    private readonly ILoyaltyCardRepository _cards;
    private readonly IUnitOfWork _uow;

    public DeleteCustomerHandler(
        ICustomerRepository customers,
        ILoyaltyCardRepository cards,
        IUnitOfWork uow)
    {
        _customers = customers;
        _cards = cards;
        _uow = uow;
    }

    public async Task<Result> Handle(DeleteCustomerCommand command, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(command.CustomerId, ct);
        if (customer is null)
            return Result.Fail("Cliente no encontrado.");

        var card = await _cards.GetByCustomerIdAsync(customer.Id, ct);

        if (customer.IsActive)
        {
            customer.Deactivate();
            _customers.Update(customer);
        }

        if (card is not null && card.IsActive)
        {
            card.Deactivate();
            _cards.Update(card);
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
