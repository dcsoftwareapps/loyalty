namespace KBeauty.Loyalty.Application.Notifications.PointsExpiration;

public sealed record PointsExpirationNotificationCandidateDto(
    Guid CustomerId,
    Guid LoyaltyCardId,
    string CustomerName,
    string SerialNumber,
    DateOnly ExpirationDate,
    int PointsExpiring,
    string CorrelationId,
    bool AlreadyNotified);

public sealed record CreatePointExpirationNotificationsResponse(
    DateTime RunAtUtc,
    DateOnly TargetExpirationDate,
    int CandidatesFound,
    int NotificationsCreated,
    int AlreadyNotified,
    IReadOnlyList<string> Warnings);
