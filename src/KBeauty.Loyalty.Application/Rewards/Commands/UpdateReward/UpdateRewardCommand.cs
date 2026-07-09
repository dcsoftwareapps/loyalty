using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Rewards.Commands.UpdateReward;

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
