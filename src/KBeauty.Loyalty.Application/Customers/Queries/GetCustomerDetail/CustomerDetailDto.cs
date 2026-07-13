using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Application.Customers.Queries.GetCustomerDetail;

public sealed record CustomerDetailDto(
    CustomerSummaryDto Summary,
    CustomerWalletDto Wallet,
    CustomerStatisticsDto Statistics,
    IReadOnlyList<CustomerPointHistoryItemDto> PointHistory,
    IReadOnlyList<CustomerRedemptionHistoryItemDto> RedemptionHistory);

public sealed record CustomerSummaryDto(
    Guid CustomerId,
    string FullName,
    string Email,
    string? Phone,
    DateTime CreatedAt,
    bool IsActive,
    string Level,
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
    int LifetimePoints,
    int PointsRedeemed,
    int TotalRedemptions,
    int PendingRedemptions,
    int CancelledRedemptions,
    int ConfirmedRedemptions);

public sealed record CustomerPointHistoryItemDto(
    DateTime CreatedAt,
    TransactionType Type,
    string Description,
    int Points,
    int? BalanceAfter);

public sealed record CustomerRedemptionHistoryItemDto(
    DateTime RedeemedAt,
    string RewardName,
    RedemptionStatus Status,
    int PointsSpent);
