using KBeauty.Loyalty.Application.Notifications.Custom;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Custom.Queries.PreviewCustomNotificationAudience;

public sealed record PreviewCustomNotificationAudienceQuery(
    CustomNotificationAudienceType AudienceType,
    int? MinimumPoints,
    int? PointsExpiringDaysAhead,
    int SampleSize = 25) : IRequest<Result<CustomNotificationAudiencePreviewDto>>;
