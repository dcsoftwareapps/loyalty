using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    Guid CustomerId,
    Guid LoyaltyCardId,
    string? CustomerName,
    string? SerialNumber,
    NotificationType Type,
    string Title,
    string Message,
    NotificationStatus Status,
    DateTime CreatedAt,
    DateTime? ScheduledAtUtc,
    DateTime? DisplayUntilUtc,
    DateTime? ProcessedAt,
    string? CorrelationId,
    string? Source,
    string? FailureReason,
    IReadOnlyList<NotificationDeliveryDto> Deliveries);

public sealed record NotificationDeliveryDto(
    Guid Id,
    NotificationChannel Channel,
    NotificationDeliveryStatus Status,
    int AttemptCount,
    int DevicesFound,
    int PushesAttempted,
    int PushesAccepted,
    int PushesFailed,
    string? FailureReason);

public sealed record NotificationMetricsDto(
    int Pending,
    int Processed,
    int Failed,
    int CustomersReached,
    int PushesAttempted,
    int PushesFailed);
