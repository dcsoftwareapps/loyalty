using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Redemptions.Commands.RedeemReward;

public sealed record RedemptionResponse(
    Guid RedemptionId,
    string RewardName,
    int PointsSpent,
    int RemainingPoints,
    RedemptionStatus Status,
    DateTime RedeemedAt);
