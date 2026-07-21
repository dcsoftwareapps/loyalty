using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Notifications.PointCampaign;

public sealed record PointCampaignNotificationPreviewDto(
    DateTime RunAtUtc,
    int ActiveCampaignsFound,
    int CardsEvaluated,
    int CardsEligible,
    IReadOnlyList<PointCampaignNotificationCandidateDto> Candidates);

public sealed record PointCampaignNotificationCandidateDto(
    Guid CustomerId,
    Guid LoyaltyCardId,
    string CustomerName,
    string SerialNumber,
    string Level,
    Guid CampaignId,
    string CampaignName,
    int Multiplier,
    decimal? MinimumPurchaseAmount,
    CampaignLevelEligibility LevelEligibility,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime EndsAtLocal,
    string CorrelationId,
    bool AlreadyNotified);

public sealed record CreatePointCampaignStartedNotificationsResponse(
    DateTime RunAtUtc,
    int ActiveCampaignsFound,
    int CardsEvaluated,
    int CardsEligible,
    int NotificationsCreated,
    int AlreadyNotified,
    IReadOnlyList<string> Warnings);
