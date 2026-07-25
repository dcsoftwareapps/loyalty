using LoyaltyCloud.Application.Notifications.Commands.CreateMonthlyProductStartedNotifications;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Rewards.Commands.ActivateReward;

public sealed class ActivateRewardHandler : IRequestHandler<ActivateRewardCommand, Result<RewardAdminDto>>
{
    private readonly IRewardCatalogRepository _rewards;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ISender _sender;
    private readonly ILogger<ActivateRewardHandler> _logger;

    public ActivateRewardHandler(
        IRewardCatalogRepository rewards,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ISender sender,
        ILogger<ActivateRewardHandler> logger)
    {
        _rewards = rewards;
        _dt = dt;
        _uow = uow;
        _sender = sender;
        _logger = logger;
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

        await TriggerMonthlyProductNotificationsIfActiveAsync(reward, ct);

        return Result.Ok(reward.ToAdminDto(_dt.UtcNow));
    }

    private async Task TriggerMonthlyProductNotificationsIfActiveAsync(Domain.Entities.RewardCatalogItem reward, CancellationToken ct)
    {
        var nowUtc = _dt.UtcNow;
        var isAvailable = reward.IsMonthlyProduct && reward.IsAvailableOn(nowUtc);
        _logger.LogInformation(
            "Immediate MonthlyProduct notification trigger. Tenant={TenantId}, RewardId={RewardId}, RewardName={RewardName}, IsMonthlyProduct={IsMonthlyProduct}, IsActive={IsActive}, ValidFromUtc={ValidFromUtc:O}, ValidToUtc={ValidToUtc:O}, NowUtc={NowUtc:O}, IsCurrentlyActive={IsCurrentlyActive}.",
            reward.TenantId,
            reward.Id,
            reward.Name,
            reward.IsMonthlyProduct,
            reward.IsActive,
            reward.ValidFrom,
            reward.ValidTo,
            nowUtc,
            isAvailable);

        if (!isAvailable)
            return;

        try
        {
            var result = await _sender.Send(new CreateMonthlyProductStartedNotificationsCommand("reward-admin", RewardId: reward.Id), ct);
            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Immediate monthly product notification scan failed after reward activate. reward={RewardId}, error={Error}.",
                    reward.Id,
                    result.Error);
                return;
            }

            _logger.LogInformation(
                "Directed MonthlyProduct notification generation completed after reward activate. RewardId={RewardId}, EligibleCards={EligibleCards}, Created={Created}, AlreadyNotified={AlreadyNotified}.",
                reward.Id,
                result.Value.CardsEligible,
                result.Value.NotificationsCreated,
                result.Value.AlreadyNotified);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Immediate monthly product notification scan failed after reward activate. reward={RewardId}.",
                reward.Id);
        }
    }
}
