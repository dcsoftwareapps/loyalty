using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Redemptions.Queries.ListRedemptions;

public sealed record RedemptionHistoryItemDto(
    Guid RedemptionId,
    string CustomerName,
    string SerialNumber,
    string RewardName,
    int PointsSpent,
    RedemptionStatus Status,
    DateTime RedeemedAt,
    DateTime? ResolvedAt,
    string? ResolvedBy,
    string? Notes);
