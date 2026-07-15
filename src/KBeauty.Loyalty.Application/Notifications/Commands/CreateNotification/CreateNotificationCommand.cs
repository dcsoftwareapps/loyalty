using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CreateNotification;

public sealed record CreateNotificationCommand(
    string SerialNumber,
    NotificationType Type,
    string Title,
    string Message,
    DateTime? ScheduledAtUtc,
    DateTime? DisplayUntilUtc,
    IReadOnlyList<NotificationChannel> Channels,
    string? CorrelationId,
    string? Source,
    string? MetadataJson,
    bool ProcessImmediately = true) : IRequest<Result<NotificationDto>>;
