using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Queries.GetNotificationMetrics;

public sealed record GetNotificationMetricsQuery : IRequest<Result<NotificationMetricsDto>>;
