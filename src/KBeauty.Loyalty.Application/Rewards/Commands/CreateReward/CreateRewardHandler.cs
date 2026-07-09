using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;

namespace KBeauty.Loyalty.Application.Rewards.Commands.CreateReward;

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
        // TODO Phase 2.x: Enforce only one active monthly product when product-month rules are finalized.
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
