using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Rewards.Commands.UpdateReward;

public sealed class UpdateRewardHandler : IRequestHandler<UpdateRewardCommand, Result<RewardAdminDto>>
{
    private readonly IRewardCatalogRepository _rewards;
    private readonly ITenantLoyaltyLevelReadService _tenantLevels;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;

    public UpdateRewardHandler(
        IRewardCatalogRepository rewards,
        ITenantLoyaltyLevelReadService tenantLevels,
        IDateTimeProvider dt,
        IUnitOfWork uow)
    {
        _rewards = rewards;
        _tenantLevels = tenantLevels;
        _dt = dt;
        _uow = uow;
    }

    public async Task<Result<RewardAdminDto>> Handle(UpdateRewardCommand command, CancellationToken ct)
    {
        var reward = await _rewards.GetByIdAsync(command.Id, ct);
        if (reward is null)
            return Result.Fail<RewardAdminDto>($"No se encontro recompensa con id '{command.Id}'.");

        var tenantLevels = await _tenantLevels.GetActiveLevelsAsync(ct);
        var levelError = RewardLevelRules.TryCanonicalizeMinimumLevel(
            command.MinLevel,
            tenantLevels,
            out var canonicalMinLevel);
        if (levelError is not null)
            return Result.Fail<RewardAdminDto>(levelError);

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
            canonicalMinLevel,
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
