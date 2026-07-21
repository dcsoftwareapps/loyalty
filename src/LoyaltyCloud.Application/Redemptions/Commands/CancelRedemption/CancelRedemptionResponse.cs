using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Redemptions.Commands.CancelRedemption;

public sealed record CancelRedemptionResponse(
    Guid RedemptionId,
    RedemptionStatus Status,
    int PointsRestored,
    int CurrentPoints,
    DateTime? CancelledAt,
    string? RewardName);
