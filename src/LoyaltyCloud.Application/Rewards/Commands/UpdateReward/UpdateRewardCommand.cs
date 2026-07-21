using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Rewards.Commands.UpdateReward;

public sealed record UpdateRewardCommand(
    Guid Id,
    string Name,
    string Description,
    int PointsCost,
    string MinLevel,
    bool IsMonthlyProduct,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    bool IsActive) : IRequest<Result<RewardAdminDto>>;
