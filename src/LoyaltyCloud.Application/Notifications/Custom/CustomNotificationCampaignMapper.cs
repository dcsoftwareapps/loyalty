using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Application.Notifications.Custom;

internal static class CustomNotificationCampaignMapper
{
    public static CustomNotificationCampaignDto ToDto(CustomNotificationCampaign campaign) =>
        new(
            campaign.Id,
            campaign.Name,
            campaign.Title,
            campaign.ShortMessage,
            campaign.LongMessage,
            campaign.AudienceType,
            campaign.MinimumPoints,
            campaign.PointsExpiringDaysAhead,
            campaign.ScheduledAtUtc,
            campaign.DisplayUntilUtc,
            campaign.Status,
            campaign.CreatedAt,
            campaign.StartedAt,
            campaign.CompletedAt,
            campaign.CancelledAt,
            campaign.IntendedRecipients,
            campaign.NotificationsCreated,
            campaign.NotificationsSucceeded,
            campaign.NotificationsFailed,
            campaign.FailureReason);
}
