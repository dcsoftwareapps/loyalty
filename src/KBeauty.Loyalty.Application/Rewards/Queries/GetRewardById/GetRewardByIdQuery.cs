using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Rewards.Queries.GetRewardById;

public sealed record GetRewardByIdQuery(Guid Id) : IRequest<Result<RewardAdminDto>>;
