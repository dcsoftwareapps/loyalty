using FluentValidation;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.UpdateTenantGracePeriod;

internal sealed class UpdateTenantGracePeriodValidator : AbstractValidator<UpdateTenantGracePeriodCommand>
{
    public UpdateTenantGracePeriodValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.NewGracePeriodEndUtc)
            .Must(date => !date.HasValue || date.Value.Kind != DateTimeKind.Local)
            .WithMessage("La fecha de fin de gracia debe estar en UTC.");
    }
}
