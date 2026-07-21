using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Enums;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Commands.CreateCustomNotificationCampaign;

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
