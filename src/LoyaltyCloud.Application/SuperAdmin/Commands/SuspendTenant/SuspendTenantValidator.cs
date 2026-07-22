using FluentValidation;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.SuspendTenant;

internal sealed class SuspendTenantValidator : AbstractValidator<SuspendTenantCommand>
{
    public SuspendTenantValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
    }
}
