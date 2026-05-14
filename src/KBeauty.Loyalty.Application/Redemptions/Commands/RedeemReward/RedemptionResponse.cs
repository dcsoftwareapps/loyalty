using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Application.Redemptions.Commands.RedeemReward;

public sealed record RedemptionResponse(
    Guid RedemptionId,
    string RewardName,
    int PointsSpent,
    int RemainingPoints,
    RedemptionStatus Status,
    DateTime RedeemedAt);
