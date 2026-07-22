using FluentValidation;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.CancelTenant;

internal sealed class CancelTenantValidator : AbstractValidator<CancelTenantCommand>
{
    public CancelTenantValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
    }
}
