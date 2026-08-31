using LoyaltyCloud.Application.Common.Branding;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
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

        RuleFor(x => x.AppleWalletPrimaryContentMode)
            .Must(value => string.IsNullOrWhiteSpace(value) ||
                           Enum.TryParse<AppleWalletPrimaryContentMode>(value, ignoreCase: true, out _))
            .WithMessage("El contenido principal de Apple Wallet no es valido.");
    }
}
