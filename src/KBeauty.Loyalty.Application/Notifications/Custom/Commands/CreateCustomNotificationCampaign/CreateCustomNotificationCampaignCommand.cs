using KBeauty.Loyalty.Application.Notifications.Custom;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Custom.Commands.CreateCustomNotificationCampaign;

public sealed record CreateCustomNotificationCampaignCommand(
    string Name,
    string Title,
    string ShortMessage,
    string LongMessage,
    CustomNotificationAudienceType AudienceType,
    int? MinimumPoints,
    int? PointsExpiringDaysAhead,
    DateTime? ScheduledAtUtc,
    DateTime DisplayUntilUtc,
    bool SendImmediately) : IRequest<Result<CustomNotificationCampaignDto>>;
