using FluentValidation;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.RecordManualSubscriptionPayment;

internal sealed class RecordManualSubscriptionPaymentValidator
    : AbstractValidator<RecordManualSubscriptionPaymentCommand>
{
    private static readonly int[] AllowedMonths = [1, 3, 6, 12];

    public RecordManualSubscriptionPaymentValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.Months)
            .Must(months => AllowedMonths.Contains(months))
            .WithMessage("Meses permitidos: 1, 3, 6 o 12.");
    }
}
