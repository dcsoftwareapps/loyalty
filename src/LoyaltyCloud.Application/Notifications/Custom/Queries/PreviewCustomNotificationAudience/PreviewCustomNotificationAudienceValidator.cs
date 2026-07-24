using FluentValidation;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Application.Notifications.Custom.Queries.PreviewCustomNotificationAudience;

internal sealed class PreviewCustomNotificationAudienceValidator : AbstractValidator<PreviewCustomNotificationAudienceQuery>
{
    public PreviewCustomNotificationAudienceValidator(ITenantLoyaltyLevelReadService levels)
    {
        RuleFor(x => x.AudienceType)
            .MustAsync((audienceType, ct) => IsValidAudienceTypeAsync(audienceType, levels, ct))
            .WithMessage("AudienceType debe ser una audiencia soportada o un nivel activo del tenant actual.");
        RuleFor(x => x.MinimumPoints)
            .GreaterThanOrEqualTo(0)
            .When(x => CustomNotificationCampaign.IsMinimumPointsAudience(x.AudienceType));
        RuleFor(x => x.PointsExpiringDaysAhead)
            .GreaterThan(0)
            .When(x => CustomNotificationCampaign.IsPointsExpiringAudience(x.AudienceType));
        RuleFor(x => x.SampleSize).InclusiveBetween(1, 100);
    }

    private static async Task<bool> IsValidAudienceTypeAsync(
        string? audienceType,
        ITenantLoyaltyLevelReadService levels,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(audienceType))
            return false;

        var value = audienceType.Trim();
        if (CustomNotificationCampaign.IsAllWalletUsersAudience(value) ||
            CustomNotificationCampaign.IsMinimumPointsAudience(value) ||
            CustomNotificationCampaign.IsPointsExpiringAudience(value))
            return true;

        if (value.Length > 50)
            return false;

        var activeLevels = await levels.GetActiveLevelsAsync(ct);
        return activeLevels.Any(level =>
            string.Equals(level.Name, value, StringComparison.OrdinalIgnoreCase));
    }
}
