using FluentValidation;

namespace LoyaltyCloud.Application.Branding.Commands.UploadTenantWalletLogo;

public sealed class UploadTenantWalletLogoValidator : AbstractValidator<UploadTenantWalletLogoCommand>
{
    public UploadTenantWalletLogoValidator()
    {
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.ContentType).NotEmpty();
        RuleFor(x => x.ContentLength).GreaterThan(0);
    }
}
