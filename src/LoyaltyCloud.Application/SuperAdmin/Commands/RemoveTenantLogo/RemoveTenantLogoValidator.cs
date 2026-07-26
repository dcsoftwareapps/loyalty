using FluentValidation;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.RemoveTenantLogo;

public sealed class RemoveTenantLogoValidator : AbstractValidator<RemoveTenantLogoCommand>
{
    public RemoveTenantLogoValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}
