using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

public class LoyaltyNotification : Entity
{
    private readonly List<NotificationDelivery> _deliveries = new();

    public Guid CustomerId { get; private set; }
    public Guid LoyaltyCardId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ScheduledAtUtc { get; private set; }
    public DateTime? ProcessingStartedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime? DisplayUntilUtc { get; private set; }
    public Guid? CustomNotificationCampaignId { get; private set; }
    public string? ShortMessage { get; private set; }
    public string? LongMessage { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? Source { get; private set; }
    public string? MetadataJson { get; private set; }
    public string? FailureReason { get; private set; }
    public IReadOnlyCollection<NotificationDelivery> Deliveries => _deliveries.AsReadOnly();

    private LoyaltyNotification() { }

    public LoyaltyNotification(
        Guid id,
        Guid customerId,
        Guid loyaltyCardId,
        NotificationType type,
        string title,
        string message,
        DateTime createdAtUtc,
        DateTime? scheduledAtUtc,
        DateTime? displayUntilUtc,
        string? correlationId,
        string? source,
        string? metadataJson,
        Guid? customNotificationCampaignId = null,
        string? shortMessage = null,
        string? longMessage = null) : base(id)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId requerido.", nameof(customerId));
        if (loyaltyCardId == Guid.Empty) throw new ArgumentException("LoyaltyCardId requerido.", nameof(loyaltyCardId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Titulo requerido.", nameof(title));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Mensaje requerido.", nameof(message));
        if (title.Length > 200) throw new ArgumentOutOfRangeException(nameof(title), "Titulo demasiado largo.");
        if (message.Length > 1000) throw new ArgumentOutOfRangeException(nameof(message), "Mensaje demasiado largo.");

        CustomerId = customerId;
        LoyaltyCardId = loyaltyCardId;
        Type = type;
        Title = title.Trim();
        Message = Sanitize(message);
        Status = NotificationStatus.Pending;
        CreatedAt = createdAtUtc;
        ScheduledAtUtc = scheduledAtUtc;
        DisplayUntilUtc = displayUntilUtc;
        CustomNotificationCampaignId = customNotificationCampaignId;
        ShortMessage = SanitizeOptional(shortMessage);
        LongMessage = SanitizeOptional(longMessage);
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim();
    }

    public void AddDelivery(NotificationDelivery delivery) => _deliveries.Add(delivery);

    public void MarkProcessing(DateTime nowUtc)
    {
        Status = NotificationStatus.Processing;
        ProcessingStartedAt = nowUtc;
        FailureReason = null;
    }

    public void MarkCompleted(NotificationStatus status, DateTime nowUtc, string? failureReason = null)
    {
        Status = status;
        ProcessedAt = nowUtc;
        FailureReason = Truncate(failureReason, 1000);
    }

    public void MarkPendingForRetry()
    {
        Status = NotificationStatus.Pending;
        ProcessingStartedAt = null;
        ProcessedAt = null;
        FailureReason = null;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status is NotificationStatus.Delivered or NotificationStatus.PartiallyDelivered)
            throw new InvalidOperationException("No se puede cancelar una notificacion ya procesada.");

        Status = NotificationStatus.Cancelled;
        CancelledAt = nowUtc;
        ProcessedAt = nowUtc;
    }

    private static string Sanitize(string value) =>
        value.Trim().Replace("<", string.Empty).Replace(">", string.Empty);

    private static string? SanitizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Sanitize(value);

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed[..Math.Min(trimmed.Length, max)];
    }
}
