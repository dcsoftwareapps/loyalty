using FluentValidation;

namespace KBeauty.Loyalty.Application.Customers.Commands.UpdateCustomerBirthday;

public sealed class UpdateCustomerBirthdayValidator : AbstractValidator<UpdateCustomerBirthdayCommand>
{
    public UpdateCustomerBirthdayValidator()
    {
        RuleFor(x => x)
            .Must(x => x.CustomerId.HasValue || !string.IsNullOrWhiteSpace(x.SerialNumber))
            .WithMessage("Debe indicar customerId o serialNumber.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("El mes debe estar entre 1 y 12.");

        RuleFor(x => x.Day)
            .Must((command, day) => command.Month is >= 1 and <= 12
                && day >= 1
                && day <= DateTime.DaysInMonth(2000, command.Month))
            .WithMessage("El dia no es valido para el mes seleccionado.");
    }
}
