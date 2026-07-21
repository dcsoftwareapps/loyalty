using FluentValidation;
using LoyaltyCloud.Common.Constants;

namespace LoyaltyCloud.Application.Config.Commands.UpdateProgramConfig;

public sealed class UpdateProgramConfigValidator : AbstractValidator<UpdateProgramConfigCommand>
{
    public UpdateProgramConfigValidator()
    {
        RuleFor(x => x.Entries)
            .NotNull().WithMessage("Lista de entradas requerida.")
            .Must(e => e.Count > 0).WithMessage("Debe haber al menos una entrada para actualizar.");

        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Key)
                .NotEmpty().WithMessage("Cada entrada debe tener Key.")
                .MaximumLength(100);
            entry.RuleFor(e => e.Value)
                .NotNull().WithMessage("Cada entrada debe tener Value (puede ser cadena vacía).")
                .MaximumLength(500);
            entry.RuleFor(e => e.Value)
                .Must(v => bool.TryParse(v, out _))
                .When(e => string.Equals(e.Key, LoyaltyConstants.ConfigKeys.PointsExpirationEnabled, StringComparison.OrdinalIgnoreCase))
                .WithMessage("points_expiration_enabled debe ser true o false.");
            entry.RuleFor(e => e.Value)
                .Must(v => int.TryParse(v, out var months) && months > 0)
                .When(e => string.Equals(e.Key, LoyaltyConstants.ConfigKeys.PointsExpireAfterMonths, StringComparison.OrdinalIgnoreCase))
                .WithMessage("points_expire_after_months debe ser un entero mayor a 0.");
        });

        RuleFor(x => x.UpdatedBy)
            .NotEmpty().WithMessage("UpdatedBy requerido para auditoría.");
    }
}
