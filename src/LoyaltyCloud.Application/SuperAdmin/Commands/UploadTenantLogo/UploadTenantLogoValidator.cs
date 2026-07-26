using FluentValidation;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.UploadTenantLogo;

public sealed class UploadTenantLogoValidator : AbstractValidator<UploadTenantLogoCommand>
{
    public UploadTenantLogoValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ContentLength).GreaterThan(0);
        RuleFor(x => x.Content).NotNull();
    }
}
