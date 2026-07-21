using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

public class CustomNotificationCampaign : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string ShortMessage { get; private set; } = string.Empty;
    public string LongMessage { get; private set; } = string.Empty;
    public CustomNotificationAudienceType AudienceType { get; private set; }
    public int? MinimumPoints { get; private set; }
    public int? PointsExpiringDaysAhead { get; private set; }
    public DateTime? ScheduledAtUtc { get; private set; }
    public DateTime DisplayUntilUtc { get; private set; }
    public CustomNotificationCampaignStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public int IntendedRecipients { get; private set; }
    public int NotificationsCreated { get; private set; }
    public int NotificationsSucceeded { get; private set; }
    public int NotificationsFailed { get; private set; }
    public string? FailureReason { get; private set; }

    private CustomNotificationCampaign() { }

    public CustomNotificationCampaign(
        Guid id,
        Guid tenantId,
        string name,
        string title,
        string shortMessage,
        string longMessage,
        CustomNotificationAudienceType audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        DateTime? scheduledAtUtc,
        DateTime displayUntilUtc,
        DateTime createdAtUtc) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido.", nameof(tenantId));
        var cleanName = Sanitize(name);
        var cleanTitle = Sanitize(title);
        var cleanShortMessage = Sanitize(shortMessage);
        var cleanLongMessage = Sanitize(longMessage);
        Validate(cleanName, cleanTitle, cleanShortMessage, cleanLongMessage, audienceType, minimumPoints, pointsExpiringDaysAhead, scheduledAtUtc, displayUntilUtc, createdAtUtc);

        TenantId = tenantId;
        Name = cleanName;
        Title = cleanTitle;
        ShortMessage = cleanShortMessage;
        LongMessage = cleanLongMessage;
        AudienceType = audienceType;
        MinimumPoints = minimumPoints;
        PointsExpiringDaysAhead = pointsExpiringDaysAhead;
        ScheduledAtUtc = scheduledAtUtc;
        DisplayUntilUtc = displayUntilUtc;
        Status = scheduledAtUtc.HasValue && scheduledAtUtc.Value > createdAtUtc
            ? CustomNotificationCampaignStatus.Scheduled
            : CustomNotificationCampaignStatus.Draft;
        CreatedAt = createdAtUtc;
    }

    public void MarkProcessing(DateTime nowUtc, int intendedRecipients)
    {
        if (Status == CustomNotificationCampaignStatus.Cancelled)
            throw new InvalidOperationException("No se puede procesar una campana cancelada.");

        Status = CustomNotificationCampaignStatus.Processing;
        StartedAt ??= nowUtc;
        IntendedRecipients = intendedRecipients;
        FailureReason = null;
    }

    public void MarkCompleted(DateTime nowUtc, int created, int succeeded, int failed, string? failureReason = null)
    {
        NotificationsCreated = created;
        NotificationsSucceeded = succeeded;
        NotificationsFailed = failed;
        CompletedAt = nowUtc;
        FailureReason = Truncate(failureReason, 1000);
        Status = failed == 0
            ? CustomNotificationCampaignStatus.Completed
            : created > 0 || succeeded > 0
                ? CustomNotificationCampaignStatus.PartiallyCompleted
                : CustomNotificationCampaignStatus.Failed;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status is CustomNotificationCampaignStatus.Completed or CustomNotificationCampaignStatus.PartiallyCompleted)
            throw new InvalidOperationException("No se puede cancelar una campana ya completada.");

        Status = CustomNotificationCampaignStatus.Cancelled;
        CancelledAt = nowUtc;
        CompletedAt ??= nowUtc;
    }

    public bool IsDue(DateTime nowUtc) =>
        Status == CustomNotificationCampaignStatus.Scheduled &&
        ScheduledAtUtc.HasValue &&
        ScheduledAtUtc.Value <= nowUtc;

    private static void Validate(
        string name,
        string title,
        string shortMessage,
        string longMessage,
        CustomNotificationAudienceType audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        DateTime? scheduledAtUtc,
        DateTime displayUntilUtc,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nombre interno requerido.", nameof(name));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Titulo requerido.", nameof(title));
        if (title.Trim().Length > 80)
            throw new ArgumentOutOfRangeException(nameof(title), "El titulo no puede exceder 80 caracteres.");
        if (string.IsNullOrWhiteSpace(shortMessage))
            throw new ArgumentException("Mensaje corto requerido.", nameof(shortMessage));
        if (shortMessage.Trim().Length > 40)
            throw new ArgumentOutOfRangeException(nameof(shortMessage), "El mensaje corto no puede exceder 40 caracteres.");
        if (shortMessage.Contains('\n') || shortMessage.Contains('\r') || shortMessage.Contains('\t'))
            throw new ArgumentException("El mensaje corto no admite saltos de linea ni tabs.", nameof(shortMessage));
        if (string.IsNullOrWhiteSpace(longMessage))
            throw new ArgumentException("Mensaje largo requerido.", nameof(longMessage));
        if (longMessage.Trim().Length > 500)
            throw new ArgumentOutOfRangeException(nameof(longMessage), "El mensaje largo no puede exceder 500 caracteres.");
        if (!Enum.IsDefined(typeof(CustomNotificationAudienceType), audienceType))
            throw new ArgumentException("Audiencia invalida.", nameof(audienceType));
        if (minimumPoints.HasValue && minimumPoints.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumPoints), "El monto minimo debe ser mayor o igual a cero.");
        if (pointsExpiringDaysAhead.HasValue && pointsExpiringDaysAhead.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(pointsExpiringDaysAhead), "DaysAhead debe ser mayor a cero.");
        var sendAt = scheduledAtUtc ?? nowUtc;
        if (displayUntilUtc <= sendAt)
            throw new ArgumentException("La fecha de visualizacion debe ser posterior al envio.", nameof(displayUntilUtc));
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed[..Math.Min(trimmed.Length, max)];
    }

    private static string Sanitize(string value) =>
        value.Trim().Replace("<", string.Empty, StringComparison.Ordinal)
            .Replace(">", string.Empty, StringComparison.Ordinal);
}
