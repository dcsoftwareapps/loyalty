using LoyaltyCloud.Application.Common.Branding;
using LoyaltyCloud.Domain.Entities;
using FluentValidation;

namespace LoyaltyCloud.Application.Branding.Commands.UpdateWalletCardBranding;

public sealed class UpdateWalletCardBrandingValidator : AbstractValidator<UpdateWalletCardBrandingCommand>
{
    public UpdateWalletCardBrandingValidator()
    {
        RuleFor(x => x.WalletBackgroundColor)
            .Must(value => string.IsNullOrWhiteSpace(value) || WalletColorContrast.IsHexColor(value))
            .WithMessage("El color de la tarjeta debe usar formato #RRGGBB.");

        RuleFor(x => x.WalletLogoScalePercent)
            .InclusiveBetween(
                TenantBranding.MinWalletLogoScalePercent,
                TenantBranding.MaxWalletLogoScalePercent)
            .When(x => x.WalletLogoScalePercent.HasValue)
            .WithMessage(
                $"El tamaño del logo debe estar entre {TenantBranding.MinWalletLogoScalePercent}% y {TenantBranding.MaxWalletLogoScalePercent}%.");
    }
}
