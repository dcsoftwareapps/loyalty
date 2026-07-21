using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Rewards.Queries.GetRewardById;

public sealed record GetRewardByIdQuery(Guid Id) : IRequest<Result<RewardAdminDto>>;
