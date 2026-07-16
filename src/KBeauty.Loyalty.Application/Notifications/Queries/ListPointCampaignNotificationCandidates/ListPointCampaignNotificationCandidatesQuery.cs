using KBeauty.Loyalty.Application.Notifications.PointCampaign;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Queries.ListPointCampaignNotificationCandidates;

public sealed record ListPointCampaignNotificationCandidatesQuery(
    string TimeZoneId = "America/Tijuana",
    bool IncludeAlreadyNotified = false) : IRequest<Result<PointCampaignNotificationPreviewDto>>;
