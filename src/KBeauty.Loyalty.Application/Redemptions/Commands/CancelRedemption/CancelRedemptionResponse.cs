using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Application.Redemptions.Commands.CancelRedemption;

public sealed record CancelRedemptionResponse(
    Guid RedemptionId,
    RedemptionStatus Status,
    int PointsRestored,
    int CurrentPoints,
    DateTime? CancelledAt,
    string? RewardName);
