namespace KBeauty.Loyalty.Application.Notifications.MonthlyProduct;

public sealed record MonthlyProductNotificationPreviewDto(
    DateTime RunAtUtc,
    Guid? MonthlyProductId,
    string? MonthlyProductName,
    int? PointsCost,
    DateTime? ValidFromUtc,
    DateTime? ValidToUtc,
    DateOnly? ValidToLocalDate,
    int CardsEligible,
    IReadOnlyList<MonthlyProductNotificationCandidateDto> Candidates);

public sealed record MonthlyProductNotificationCandidateDto(
    Guid CustomerId,
    Guid LoyaltyCardId,
    string CustomerName,
    string SerialNumber,
    Guid MonthlyProductId,
    string MonthlyProductName,
    int PointsCost,
    DateTime ValidToUtc,
    DateOnly ValidToLocalDate,
    string CorrelationId,
    bool AlreadyNotified);

public sealed record CreateMonthlyProductStartedNotificationsResponse(
    DateTime RunAtUtc,
    Guid? MonthlyProductId,
    string? MonthlyProductName,
    int CardsEligible,
    int NotificationsCreated,
    int AlreadyNotified,
    IReadOnlyList<string> Warnings);
