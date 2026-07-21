using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Rewards.Queries.ListRewards;

public sealed record ListRewardsQuery(
    bool ActiveOnly = false,
    bool IncludeExpired = true,
    string? MinLevel = null) : IRequest<Result<IReadOnlyList<RewardAdminDto>>>;
