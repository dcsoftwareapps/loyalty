using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Rewards.Commands.CreateReward;

public sealed record CreateRewardCommand(
    string Name,
    string Description,
    int PointsCost,
    string MinLevel,
    bool IsMonthlyProduct,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    bool IsActive) : IRequest<Result<RewardAdminDto>>;
