using FluentValidation;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.DeleteTenant;

internal sealed class DeleteTenantValidator : AbstractValidator<DeleteTenantCommand>
{
    public DeleteTenantValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.ConfirmationSlug).NotEmpty().MaximumLength(100);
    }
}
