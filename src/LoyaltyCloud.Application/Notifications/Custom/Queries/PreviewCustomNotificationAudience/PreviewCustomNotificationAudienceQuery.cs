using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Enums;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Queries.PreviewCustomNotificationAudience;

public sealed record PreviewCustomNotificationAudienceQuery(
    CustomNotificationAudienceType AudienceType,
    int? MinimumPoints,
    int? PointsExpiringDaysAhead,
    int SampleSize = 25) : IRequest<Result<CustomNotificationAudiencePreviewDto>>;
