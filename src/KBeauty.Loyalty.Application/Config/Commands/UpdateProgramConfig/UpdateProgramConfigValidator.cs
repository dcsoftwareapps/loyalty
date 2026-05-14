using FluentValidation;

namespace KBeauty.Loyalty.Application.Config.Commands.UpdateProgramConfig;

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
        });

        RuleFor(x => x.UpdatedBy)
            .NotEmpty().WithMessage("UpdatedBy requerido para auditoría.");
    }
}
