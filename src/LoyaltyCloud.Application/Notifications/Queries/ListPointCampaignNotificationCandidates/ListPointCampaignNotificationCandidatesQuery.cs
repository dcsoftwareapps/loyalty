using LoyaltyCloud.Application.Notifications.PointCampaign;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Queries.ListPointCampaignNotificationCandidates;

public sealed record ListPointCampaignNotificationCandidatesQuery(
    string TimeZoneId = "America/Tijuana",
    bool IncludeAlreadyNotified = false) : IRequest<Result<PointCampaignNotificationPreviewDto>>;
