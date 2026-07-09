using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;

namespace KBeauty.Loyalty.Application.Rewards.Queries.ListRewards;

public sealed class ListRewardsHandler
    : IRequestHandler<ListRewardsQuery, Result<IReadOnlyList<RewardAdminDto>>>
{
    private readonly IRewardCatalogRepository _rewards;
    private readonly IDateTimeProvider _dt;

    public ListRewardsHandler(IRewardCatalogRepository rewards, IDateTimeProvider dt)
    {
        _rewards = rewards;
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
            filtered = filtered.Where(r => string.Equals(r.MinLevel, query.MinLevel.Trim(), StringComparison.Ordinal));

        IReadOnlyList<RewardAdminDto> dtos = filtered
            .OrderBy(r => r.Name)
            .ThenBy(r => r.PointsCost)
            .Select(r => r.ToAdminDto(now))
            .ToList()
            .AsReadOnly();

        return Result.Ok(dtos);
    }
}
