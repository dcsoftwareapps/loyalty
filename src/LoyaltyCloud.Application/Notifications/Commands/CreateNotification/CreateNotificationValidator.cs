using FluentValidation;

namespace LoyaltyCloud.Application.Notifications.Commands.CreateNotification;

internal sealed class CreateNotificationValidator : AbstractValidator<CreateNotificationCommand>
{
    public CreateNotificationValidator()
    {
        RuleFor(x => x.SerialNumber).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Channels).NotEmpty();
        RuleFor(x => x.DisplayUntilUtc)
            .GreaterThan(DateTime.UtcNow.AddMinutes(-5))
            .When(x => x.DisplayUntilUtc.HasValue);
    }
}
