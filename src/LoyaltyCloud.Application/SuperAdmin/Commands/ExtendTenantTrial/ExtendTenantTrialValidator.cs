using FluentValidation;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.ExtendTenantTrial;

internal sealed class ExtendTenantTrialValidator : AbstractValidator<ExtendTenantTrialCommand>
{
    public ExtendTenantTrialValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.NewTrialEndUtc)
            .Must(date => date.Kind != DateTimeKind.Local)
            .WithMessage("La fecha de fin de trial debe estar en UTC.");
    }
}
