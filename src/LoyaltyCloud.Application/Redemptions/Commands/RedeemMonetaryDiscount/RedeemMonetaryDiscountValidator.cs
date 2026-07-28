using FluentValidation;

namespace LoyaltyCloud.Application.Redemptions.Commands.RedeemMonetaryDiscount;

public sealed class RedeemMonetaryDiscountValidator : AbstractValidator<RedeemMonetaryDiscountCommand>
{
    public RedeemMonetaryDiscountValidator()
    {
        RuleFor(x => x.SerialNumber)
            .NotEmpty().WithMessage("SerialNumber requerido.");

        RuleFor(x => x.PointsToRedeem)
            .GreaterThan(0).WithMessage("Los puntos a canjear deben ser mayores a 0.");
    }
}
