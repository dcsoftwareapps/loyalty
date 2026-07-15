using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Queries.ListNotifications;

public sealed record ListNotificationsQuery(
    Guid? CustomerId = null,
    NotificationType? Type = null,
    NotificationStatus? Status = null,
    NotificationChannel? Channel = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Take = 100) : IRequest<Result<IReadOnlyList<NotificationDto>>>;
