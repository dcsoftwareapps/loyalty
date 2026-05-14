using FluentValidation;

namespace KBeauty.Loyalty.Application.Redemptions.Commands.RedeemReward;

public sealed class RedeemRewardValidator : AbstractValidator<RedeemRewardCommand>
{
    public RedeemRewardValidator()
    {
        RuleFor(x => x.SerialNumber)
            .NotEmpty().WithMessage("El serial es requerido.")
            .MaximumLength(20);

        RuleFor(x => x.RewardCatalogItemId)
            .NotEmpty().WithMessage("Beneficio a canjear requerido.");

        RuleFor(x => x.OperatorId)
            .NotEmpty().WithMessage("Id de operador requerido para auditoría.");
    }
}
