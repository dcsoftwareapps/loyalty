using FluentValidation;

namespace KBeauty.Loyalty.Application.Redemptions.Commands.ConfirmRedemption;

public sealed class ConfirmRedemptionValidator : AbstractValidator<ConfirmRedemptionCommand>
{
    public ConfirmRedemptionValidator()
    {
        RuleFor(x => x.RedemptionId)
            .NotEmpty().WithMessage("RedemptionId requerido.");

        RuleFor(x => x.OperatorId)
            .NotEmpty().WithMessage("Id de operador requerido para auditoría.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
