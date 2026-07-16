namespace KBeauty.Loyalty.Application.Notifications.BirthdayBenefit;

public sealed record BirthdayBenefitNotificationPreviewDto(
    DateTime RunAtUtc,
    DateOnly LocalDate,
    int CustomersEligible,
    int Multiplier,
    DateTime DisplayUntilUtc,
    DateOnly DisplayUntilLocalDate,
    IReadOnlyList<BirthdayBenefitNotificationCandidateDto> Candidates);

public sealed record BirthdayBenefitNotificationCandidateDto(
    Guid CustomerId,
    Guid LoyaltyCardId,
    string CustomerName,
    string SerialNumber,
    DateTime BirthDate,
    int BenefitYear,
    int Multiplier,
    DateTime DisplayUntilUtc,
    DateOnly DisplayUntilLocalDate,
    string CorrelationId,
    bool AlreadyNotified);

public sealed record CreateBirthdayBenefitStartedNotificationsResponse(
    DateTime RunAtUtc,
    DateOnly LocalDate,
    int CustomersEligible,
    int NotificationsCreated,
    int AlreadyNotified,
    IReadOnlyList<string> Warnings);
