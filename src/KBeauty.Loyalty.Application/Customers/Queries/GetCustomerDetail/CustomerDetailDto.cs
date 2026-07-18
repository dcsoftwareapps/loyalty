using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Application.Customers.Queries.GetCustomerDetail;

public sealed record CustomerDetailDto(
    CustomerSummaryDto Summary,
    CustomerWalletDto Wallet,
    CustomerStatisticsDto Statistics,
    CustomerLoyaltyAuditDto LoyaltyAudit,
    IReadOnlyList<CustomerNotificationHistoryItemDto> NotificationHistory,
    IReadOnlyList<CustomerPointHistoryItemDto> PointHistory,
    IReadOnlyList<CustomerRedemptionHistoryItemDto> RedemptionHistory);

public sealed record CustomerSummaryDto(
    Guid CustomerId,
    string FullName,
    string Email,
    string? Phone,
    DateTime DateOfBirth,
    bool BirthdayCaptured,
    DateTime CreatedAt,
    bool IsActive,
    string Level,
    DateTime? LevelAchievedAt,
    bool WalletIssued);

public sealed record CustomerWalletDto(
    bool WalletIssued,
    string? SerialNumber,
    int? CurrentPoints,
    DateTime? IssuedAt,
    DateTime? LastActivityAt,
    int DeviceRegistrationCount,
    DateTime? LastPushSentAt);

public sealed record CustomerStatisticsDto(
    int CurrentPoints,
    int RollingPoints,
    int LifetimePoints,
    int PointsRedeemed,
    int TotalRedemptions,
    int PendingRedemptions,
    int CancelledRedemptions,
    int ConfirmedRedemptions);

public sealed record CustomerLoyaltyAuditDto(
    UpcomingExpirationDto? UpcomingExpiration,
    RollingProgressDto RollingProgress,
    IReadOnlyList<LotSummaryDto> Lots,
    IReadOnlyList<ConsumptionDto> Consumptions);

public sealed record UpcomingExpirationDto(
    DateTime ExpiresAt,
    int Points);

public sealed record RollingProgressDto(
    int RollingPoints,
    int GlowThreshold,
    int RadianceThreshold,
    int PointsToNextLevel,
    string CurrentLevel,
    string NextLevel);

public sealed record LotSummaryDto(
    Guid LotId,
    DateTime EarnedAt,
    DateTime ExpiresAt,
    int OriginalAmount,
    int RemainingAmount,
    string Status);

public sealed record ConsumptionDto(
    Guid ConsumptionId,
    Guid LotId,
    DateTime LotEarnedAt,
    DateTime LotExpiresAt,
    int AmountConsumed,
    int RemainingAfterConsumption,
    string Reason,
    string? RewardName,
    DateTime ConsumedAt,
    bool IsReversed);

public sealed record CustomerNotificationHistoryItemDto(
    DateTime CreatedAt,
    NotificationType Type,
    string Title,
    string Message,
    Guid? CustomNotificationCampaignId,
    string? ShortMessage,
    string? LongMessage,
    NotificationStatus Status,
    int PushesAttempted,
    int PushesAccepted,
    int PushesFailed);

public sealed record CustomerPointHistoryItemDto(
    DateTime CreatedAt,
    TransactionType Type,
    string Description,
    int Points,
    int? BalanceAfter,
    Guid? CampaignId,
    string? CampaignName,
    decimal? AppliedMultiplier);

public sealed record CustomerRedemptionHistoryItemDto(
    DateTime RedeemedAt,
    string RewardName,
    RedemptionStatus Status,
    int PointsSpent);
