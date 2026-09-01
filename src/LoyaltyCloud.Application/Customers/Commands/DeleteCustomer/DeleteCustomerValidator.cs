using FluentValidation;

namespace LoyaltyCloud.Application.Customers.Commands.DeleteCustomer;

internal sealed class DeleteCustomerValidator : AbstractValidator<DeleteCustomerCommand>
{
    public DeleteCustomerValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}
