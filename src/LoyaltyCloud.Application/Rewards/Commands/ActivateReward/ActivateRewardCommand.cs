using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Rewards.Commands.ActivateReward;

public sealed record ActivateRewardCommand(Guid Id) : IRequest<Result<RewardAdminDto>>;
