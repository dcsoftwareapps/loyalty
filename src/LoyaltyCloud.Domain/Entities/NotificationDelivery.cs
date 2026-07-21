using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

public class NotificationDelivery : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }

    public Guid LoyaltyNotificationId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationDeliveryStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? AttemptedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public int DevicesFound { get; private set; }
    public int PushesAttempted { get; private set; }
    public int PushesAccepted { get; private set; }
    public int PushesFailed { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? FailureReason { get; private set; }

    private NotificationDelivery() { }

    public NotificationDelivery(Guid id, Guid tenantId, Guid loyaltyNotificationId, NotificationChannel channel, DateTime createdAtUtc) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido.", nameof(tenantId));
        if (loyaltyNotificationId == Guid.Empty)
            throw new ArgumentException("LoyaltyNotificationId requerido.", nameof(loyaltyNotificationId));

        TenantId = tenantId;
        LoyaltyNotificationId = loyaltyNotificationId;
        Channel = channel;
        Status = NotificationDeliveryStatus.Pending;
        CreatedAt = createdAtUtc;
    }

    public void MarkProcessing(DateTime nowUtc)
    {
        Status = NotificationDeliveryStatus.Processing;
        AttemptedAt = nowUtc;
        AttemptCount++;
        FailureReason = null;
    }

    public void MarkCompleted(
        NotificationDeliveryStatus status,
        DateTime nowUtc,
        int devicesFound = 0,
        int pushesAttempted = 0,
        int pushesAccepted = 0,
        int pushesFailed = 0,
        string? providerReference = null,
        string? failureReason = null)
    {
        Status = status;
        CompletedAt = nowUtc;
        DevicesFound = devicesFound;
        PushesAttempted = pushesAttempted;
        PushesAccepted = pushesAccepted;
        PushesFailed = pushesFailed;
        ProviderReference = string.IsNullOrWhiteSpace(providerReference) ? null : providerReference.Trim();
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
    }

    public void ResetForRetry()
    {
        if (Status != NotificationDeliveryStatus.Failed)
            return;

        Status = NotificationDeliveryStatus.Pending;
        CompletedAt = null;
        FailureReason = null;
    }

    public void Cancel(DateTime nowUtc) =>
        MarkCompleted(NotificationDeliveryStatus.Cancelled, nowUtc, failureReason: "Cancelada.");
}
