using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Rewards.Queries.ListRewards;

public sealed class ListRewardsHandler
    : IRequestHandler<ListRewardsQuery, Result<IReadOnlyList<RewardAdminDto>>>
{
    private readonly IRewardCatalogRepository _rewards;
    private readonly ITenantLoyaltyLevelReadService _tenantLevels;
    private readonly IDateTimeProvider _dt;

    public ListRewardsHandler(
        IRewardCatalogRepository rewards,
        ITenantLoyaltyLevelReadService tenantLevels,
        IDateTimeProvider dt)
    {
        _rewards = rewards;
        _tenantLevels = tenantLevels;
        _dt = dt;
    }

    public async Task<Result<IReadOnlyList<RewardAdminDto>>> Handle(ListRewardsQuery query, CancellationToken ct)
    {
        var now = _dt.UtcNow;
        var rewards = await _rewards.GetAllAsync(ct);

        var filtered = rewards.AsEnumerable();

        if (query.ActiveOnly)
            filtered = filtered.Where(r => r.IsActive);

        if (!query.IncludeExpired)
            filtered = filtered.Where(r => !r.ValidTo.HasValue || r.ValidTo.Value >= now);

        if (!string.IsNullOrWhiteSpace(query.MinLevel))
        {
            var tenantLevels = await _tenantLevels.GetActiveLevelsAsync(ct);
            var levelError = RewardLevelRules.TryCanonicalizeMinimumLevel(
                query.MinLevel,
                tenantLevels,
                out var canonicalMinLevel);
            if (levelError is not null)
                return Result.Fail<IReadOnlyList<RewardAdminDto>>(levelError);

            filtered = filtered.Where(r => string.Equals(r.MinLevel, canonicalMinLevel, StringComparison.Ordinal));
        }

        IReadOnlyList<RewardAdminDto> dtos = filtered
            .OrderBy(r => r.Name)
            .ThenBy(r => r.PointsCost)
            .Select(r => r.ToAdminDto(now))
            .ToList()
            .AsReadOnly();

        return Result.Ok(dtos);
    }
}
