using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Rewards.Commands.DeactivateReward;

public sealed record DeactivateRewardCommand(Guid Id) : IRequest<Result<RewardAdminDto>>;
