using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Rewards.Commands.DeactivateReward;

public sealed record DeactivateRewardCommand(Guid Id) : IRequest<Result<RewardAdminDto>>;
