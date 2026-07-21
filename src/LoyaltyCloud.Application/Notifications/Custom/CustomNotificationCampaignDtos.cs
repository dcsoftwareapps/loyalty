using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Notifications.Custom;

public sealed record CustomNotificationCampaignDto(
    Guid Id,
    string Name,
    string Title,
    string ShortMessage,
    string LongMessage,
    CustomNotificationAudienceType AudienceType,
    int? MinimumPoints,
    int? PointsExpiringDaysAhead,
    DateTime? ScheduledAtUtc,
    DateTime DisplayUntilUtc,
    CustomNotificationCampaignStatus Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    int IntendedRecipients,
    int NotificationsCreated,
    int NotificationsSucceeded,
    int NotificationsFailed,
    string? FailureReason);

public sealed record CustomNotificationAudiencePreviewDto(
    CustomNotificationAudienceType AudienceType,
    int TotalRecipients,
    int ExcludedWithoutDeviceRegistration,
    IReadOnlyList<CustomNotificationLevelDistributionDto> LevelDistribution,
    IReadOnlyList<CustomNotificationAudienceRecipientDto> SampleRecipients,
    string Criteria,
    IReadOnlyList<string> Warnings);

public sealed record CustomNotificationAudienceRecipientDto(
    Guid CustomerId,
    Guid LoyaltyCardId,
    string CustomerName,
    string SerialNumber,
    string Level,
    int CurrentPoints,
    int DeviceRegistrationCount);

public sealed record CustomNotificationLevelDistributionDto(
    string Level,
    int Count);

public sealed record CustomNotificationCampaignProcessingDto(
    Guid CampaignId,
    CustomNotificationCampaignStatus Status,
    int IntendedRecipients,
    int NotificationsCreated,
    int NotificationsSucceeded,
    int NotificationsFailed,
    IReadOnlyList<string> Warnings);
