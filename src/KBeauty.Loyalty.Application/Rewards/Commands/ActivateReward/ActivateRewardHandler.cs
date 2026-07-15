using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;

namespace KBeauty.Loyalty.Application.Rewards.Commands.ActivateReward;

public sealed class ActivateRewardHandler : IRequestHandler<ActivateRewardCommand, Result<RewardAdminDto>>
{
    private readonly IRewardCatalogRepository _rewards;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;

    public ActivateRewardHandler(IRewardCatalogRepository rewards, IDateTimeProvider dt, IUnitOfWork uow)
    {
        _rewards = rewards;
        _dt = dt;
        _uow = uow;
    }

    public async Task<Result<RewardAdminDto>> Handle(ActivateRewardCommand command, CancellationToken ct)
    {
        var reward = await _rewards.GetByIdAsync(command.Id, ct);
        if (reward is null)
            return Result.Fail<RewardAdminDto>($"No se encontro recompensa con id '{command.Id}'.");

        if (reward.IsMonthlyProduct)
        {
            if (!reward.ValidFrom.HasValue || !reward.ValidTo.HasValue)
                return Result.Fail<RewardAdminDto>("El Producto del mes requiere fecha de inicio y fecha de fin.");

            var overlaps = await _rewards.HasOverlappingActiveMonthlyProductAsync(
                reward.ValidFrom.Value,
                reward.ValidTo.Value,
                excludeRewardId: reward.Id,
                ct);
            if (overlaps)
                return Result.Fail<RewardAdminDto>(
                    "Ya existe un Producto del mes activo con una vigencia que se traslapa.");
        }

        reward.Activate();
        _rewards.Update(reward);
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(reward.ToAdminDto(_dt.UtcNow));
    }
}
