using FluentValidation;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.ReactivateTenant;

internal sealed class ReactivateTenantValidator : AbstractValidator<ReactivateTenantCommand>
{
    public ReactivateTenantValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
    }
}
