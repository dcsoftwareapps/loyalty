using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Queries.GetNotificationMetrics;

public sealed record GetNotificationMetricsQuery : IRequest<Result<NotificationMetricsDto>>;
