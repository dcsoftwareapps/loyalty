using LoyaltyCloud.Application.Common.Branding;
using FluentValidation;

namespace LoyaltyCloud.Application.Branding.Commands.UpdateWalletCardBranding;

public sealed class UpdateWalletCardBrandingValidator : AbstractValidator<UpdateWalletCardBrandingCommand>
{
    public UpdateWalletCardBrandingValidator()
    {
        RuleFor(x => x.WalletBackgroundColor)
            .Must(value => string.IsNullOrWhiteSpace(value) || WalletColorContrast.IsHexColor(value))
            .WithMessage("El color de la tarjeta debe usar formato #RRGGBB.");
    }
}
