using FluentValidation;

namespace LoyaltyCloud.Application.Redemptions.Commands.CancelRedemption;

public sealed class CancelRedemptionValidator : AbstractValidator<CancelRedemptionCommand>
{
    public CancelRedemptionValidator()
    {
        RuleFor(x => x.RedemptionId)
            .NotEmpty().WithMessage("RedemptionId requerido.");

        RuleFor(x => x.OperatorId)
            .NotEmpty().WithMessage("Id de operador requerido para auditoria.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
