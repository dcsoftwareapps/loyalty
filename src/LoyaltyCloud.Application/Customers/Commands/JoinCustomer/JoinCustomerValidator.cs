using FluentValidation;

namespace LoyaltyCloud.Application.Customers.Commands.JoinCustomer;

public sealed class JoinCustomerValidator : AbstractValidator<JoinCustomerCommand>
{
    public JoinCustomerValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(60);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es obligatorio.")
            .MaximumLength(60);

        RuleFor(x => x).Must(x => (!x.BirthdayDay.HasValue && !x.BirthdayMonth.HasValue)
            || (x.BirthdayMonth is >= 1 and <= 12 && x.BirthdayDay >= 1
                && x.BirthdayDay <= DateTime.DaysInMonth(2000, x.BirthdayMonth.Value)))
            .WithMessage("Selecciona un día y mes de cumpleaños válidos o deja ambos vacíos.");
        RuleFor(x => x.Phone)
            .Must(phone => CustomerPhoneNormalizer.Normalize(phone).Length >= 8)
            .WithMessage("El telefono debe incluir al menos 8 digitos.")
            .MaximumLength(30)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}
