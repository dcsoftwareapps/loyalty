using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Rewards.Commands.CreateReward;

public sealed class CreateRewardHandler : IRequestHandler<CreateRewardCommand, Result<RewardAdminDto>>
{
    private readonly IRewardCatalogRepository _rewards;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;

    public CreateRewardHandler(IRewardCatalogRepository rewards, IDateTimeProvider dt, IUnitOfWork uow)
    {
        _rewards = rewards;
        _dt = dt;
        _uow = uow;
    }

    public async Task<Result<RewardAdminDto>> Handle(CreateRewardCommand command, CancellationToken ct)
    {
        if (command.IsMonthlyProduct && command.IsActive)
        {
            if (!command.ValidFrom.HasValue || !command.ValidTo.HasValue)
                return Result.Fail<RewardAdminDto>("El Producto del mes requiere fecha de inicio y fecha de fin.");

            var overlaps = await _rewards.HasOverlappingActiveMonthlyProductAsync(
                command.ValidFrom.Value,
                command.ValidTo.Value,
                excludeRewardId: null,
                ct);
            if (overlaps)
                return Result.Fail<RewardAdminDto>(
                    "Ya existe un Producto del mes activo con una vigencia que se traslapa.");
        }

        var reward = new RewardCatalogItem(
            id: Guid.NewGuid(),
            name: command.Name,
            description: command.Description,
            pointsCost: command.PointsCost,
            minLevel: command.MinLevel,
            isMonthlyProduct: command.IsMonthlyProduct,
            validFrom: command.ValidFrom,
            validTo: command.ValidTo);

        if (!command.IsActive)
            reward.Deactivate();

        await _rewards.AddAsync(reward, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(reward.ToAdminDto(_dt.UtcNow));
    }
}
