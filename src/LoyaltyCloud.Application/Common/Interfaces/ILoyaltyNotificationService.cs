using LoyaltyCloud.Application.Notifications;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ILoyaltyNotificationService
{
    Task<NotificationDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDto>> ListAsync(
        Guid? customerId,
        NotificationType? type,
        NotificationStatus? status,
        NotificationChannel? channel,
        DateTime? fromUtc,
        DateTime? toUtc,
        int take,
        CancellationToken ct = default);
    Task<NotificationMetricsDto> GetMetricsAsync(CancellationToken ct = default);
    Task<NotificationDto> CreateAsync(CreateLoyaltyNotificationRequest request, CancellationToken ct = default);
    Task<NotificationDto> ProcessAsync(Guid id, CancellationToken ct = default);
    Task<NotificationDto> RetryAsync(Guid id, CancellationToken ct = default);
    Task<NotificationDto> CancelAsync(Guid id, CancellationToken ct = default);
    Task<int> ProcessPendingAsync(int batchSize, int maxAttempts, CancellationToken ct = default);
}

public sealed record CreateLoyaltyNotificationRequest(
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
    bool ProcessImmediately,
    Guid? CustomNotificationCampaignId = null,
    string? ShortMessage = null,
    string? LongMessage = null);
