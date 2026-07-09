using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Rewards.Commands.ActivateReward;

public sealed record ActivateRewardCommand(Guid Id) : IRequest<Result<RewardAdminDto>>;
