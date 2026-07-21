using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Rewards.Queries.GetRewardById;

public sealed class GetRewardByIdHandler : IRequestHandler<GetRewardByIdQuery, Result<RewardAdminDto>>
{
    private readonly IRewardCatalogRepository _rewards;
    private readonly IDateTimeProvider _dt;

    public GetRewardByIdHandler(IRewardCatalogRepository rewards, IDateTimeProvider dt)
    {
        _rewards = rewards;
        _dt = dt;
    }

    public async Task<Result<RewardAdminDto>> Handle(GetRewardByIdQuery query, CancellationToken ct)
    {
        var reward = await _rewards.GetByIdAsync(query.Id, ct);
        if (reward is null)
            return Result.Fail<RewardAdminDto>($"No se encontro recompensa con id '{query.Id}'.");

        return Result.Ok(reward.ToAdminDto(_dt.UtcNow));
    }
}
