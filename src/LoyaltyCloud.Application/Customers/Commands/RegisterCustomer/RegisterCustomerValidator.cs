using FluentValidation;

namespace LoyaltyCloud.Application.Customers.Commands.RegisterCustomer;

/// <summary>
/// Validación sintáctica del input. Las reglas semánticas (email único,
/// referido existente) las verifica el handler con repositorios.
/// </summary>
public sealed class RegisterCustomerValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre es requerido.")
            .MaximumLength(120);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es requerido.")
            .EmailAddress().WithMessage("El email no tiene formato válido.")
            .MaximumLength(200);

        RuleFor(x => x.DateOfBirth)
            .NotEmpty()
            .Must(BeAReasonableBirthDate)
                .WithMessage("La fecha de nacimiento no es válida (debe estar entre 1900 y hoy).");

        RuleFor(x => x.Phone)
            .MaximumLength(30)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.ReferredBySerialNumber)
            .MaximumLength(20)
            .When(x => !string.IsNullOrWhiteSpace(x.ReferredBySerialNumber));
    }

    private static bool BeAReasonableBirthDate(DateTime dob) =>
        dob.Year >= 1900 && dob.Date <= DateTime.UtcNow.Date;
}
