using FluentValidation;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Notifications.Custom.Queries.PreviewCustomNotificationAudience;

internal sealed class PreviewCustomNotificationAudienceValidator : AbstractValidator<PreviewCustomNotificationAudienceQuery>
{
    public PreviewCustomNotificationAudienceValidator()
    {
        RuleFor(x => x.AudienceType).IsInEnum();
        RuleFor(x => x.MinimumPoints)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AudienceType == CustomNotificationAudienceType.MinimumPoints);
        RuleFor(x => x.PointsExpiringDaysAhead)
            .GreaterThan(0)
            .When(x => x.AudienceType == CustomNotificationAudienceType.PointsExpiring);
        RuleFor(x => x.SampleSize).InclusiveBetween(1, 100);
    }
}
