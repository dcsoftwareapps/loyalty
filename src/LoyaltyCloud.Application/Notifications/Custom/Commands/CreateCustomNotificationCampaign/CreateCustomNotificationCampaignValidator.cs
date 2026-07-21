using FluentValidation;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Notifications.Custom.Commands.CreateCustomNotificationCampaign;

internal sealed class CreateCustomNotificationCampaignValidator : AbstractValidator<CreateCustomNotificationCampaignCommand>
{
    public CreateCustomNotificationCampaignValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(80);
        RuleFor(x => x.ShortMessage)
            .NotEmpty()
            .MaximumLength(40)
            .Must(value => value is null || (!value.Contains('\n') && !value.Contains('\r') && !value.Contains('\t')))
            .WithMessage("ShortMessage no debe contener saltos de linea ni tabuladores.");
        RuleFor(x => x.LongMessage).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AudienceType).IsInEnum();
        RuleFor(x => x.MinimumPoints)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AudienceType == CustomNotificationAudienceType.MinimumPoints);
        RuleFor(x => x.MinimumPoints)
            .Null()
            .When(x => x.AudienceType != CustomNotificationAudienceType.MinimumPoints)
            .WithMessage("MinimumPoints solo aplica para audiencia MinimumPoints.");
        RuleFor(x => x.PointsExpiringDaysAhead)
            .GreaterThan(0)
            .When(x => x.AudienceType == CustomNotificationAudienceType.PointsExpiring);
        RuleFor(x => x.PointsExpiringDaysAhead)
            .Null()
            .When(x => x.AudienceType != CustomNotificationAudienceType.PointsExpiring)
            .WithMessage("PointsExpiringDaysAhead solo aplica para audiencia PointsExpiring.");
        RuleFor(x => x.DisplayUntilUtc)
            .GreaterThan(DateTime.UtcNow)
            .When(x => x.SendImmediately);
        RuleFor(x => x.DisplayUntilUtc)
            .GreaterThan(x => x.ScheduledAtUtc!.Value)
            .When(x => !x.SendImmediately && x.ScheduledAtUtc.HasValue);
        RuleFor(x => x.ScheduledAtUtc)
            .NotNull()
            .When(x => !x.SendImmediately)
            .WithMessage("ScheduledAtUtc es requerido para campanas programadas.");
        RuleFor(x => x.ScheduledAtUtc)
            .GreaterThan(DateTime.UtcNow)
            .When(x => !x.SendImmediately && x.ScheduledAtUtc.HasValue)
            .WithMessage("ScheduledAtUtc debe ser futuro para campanas programadas.");
    }
}
