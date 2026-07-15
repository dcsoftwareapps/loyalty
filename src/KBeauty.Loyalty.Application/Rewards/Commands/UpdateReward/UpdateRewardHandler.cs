using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;

namespace KBeauty.Loyalty.Application.Rewards.Commands.UpdateReward;

public sealed class UpdateRewardHandler : IRequestHandler<UpdateRewardCommand, Result<RewardAdminDto>>
{
    private readonly IRewardCatalogRepository _rewards;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;

    public UpdateRewardHandler(IRewardCatalogRepository rewards, IDateTimeProvider dt, IUnitOfWork uow)
    {
        _rewards = rewards;
        _dt = dt;
        _uow = uow;
    }

    public async Task<Result<RewardAdminDto>> Handle(UpdateRewardCommand command, CancellationToken ct)
    {
        var reward = await _rewards.GetByIdAsync(command.Id, ct);
        if (reward is null)
            return Result.Fail<RewardAdminDto>($"No se encontro recompensa con id '{command.Id}'.");

        if (command.IsMonthlyProduct && command.IsActive)
        {
            if (!command.ValidFrom.HasValue || !command.ValidTo.HasValue)
                return Result.Fail<RewardAdminDto>("El Producto del mes requiere fecha de inicio y fecha de fin.");

            var overlaps = await _rewards.HasOverlappingActiveMonthlyProductAsync(
                command.ValidFrom.Value,
                command.ValidTo.Value,
                excludeRewardId: command.Id,
                ct);
            if (overlaps)
                return Result.Fail<RewardAdminDto>(
                    "Ya existe un Producto del mes activo con una vigencia que se traslapa.");
        }

        reward.Update(
            command.Name,
            command.Description,
            command.PointsCost,
            command.MinLevel,
            command.IsMonthlyProduct,
            command.ValidFrom,
            command.ValidTo);

        if (command.IsActive)
            reward.Activate();
        else
            reward.Deactivate();

        _rewards.Update(reward);
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(reward.ToAdminDto(_dt.UtcNow));
    }
}
