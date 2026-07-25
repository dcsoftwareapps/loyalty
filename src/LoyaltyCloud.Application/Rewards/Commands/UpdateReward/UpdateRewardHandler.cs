using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.Commands.CreateMonthlyProductStartedNotifications;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Rewards.Commands.UpdateReward;

public sealed class UpdateRewardHandler : IRequestHandler<UpdateRewardCommand, Result<RewardAdminDto>>
{
    private readonly IRewardCatalogRepository _rewards;
    private readonly ITenantLoyaltyLevelReadService _tenantLevels;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ISender _sender;
    private readonly ILogger<UpdateRewardHandler> _logger;

    public UpdateRewardHandler(
        IRewardCatalogRepository rewards,
        ITenantLoyaltyLevelReadService tenantLevels,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ISender sender,
        ILogger<UpdateRewardHandler> logger)
    {
        _rewards = rewards;
        _tenantLevels = tenantLevels;
        _dt = dt;
        _uow = uow;
        _sender = sender;
        _logger = logger;
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

        await TriggerMonthlyProductNotificationsIfActiveAsync(reward, ct);

        return Result.Ok(reward.ToAdminDto(_dt.UtcNow));
    }

    private async Task TriggerMonthlyProductNotificationsIfActiveAsync(RewardCatalogItem reward, CancellationToken ct)
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
                    "Immediate monthly product notification scan failed after reward update. reward={RewardId}, error={Error}.",
                    reward.Id,
                    result.Error);
                return;
            }

            _logger.LogInformation(
                "Directed MonthlyProduct notification generation completed after reward update. RewardId={RewardId}, EligibleCards={EligibleCards}, Created={Created}, AlreadyNotified={AlreadyNotified}.",
                reward.Id,
                result.Value.CardsEligible,
                result.Value.NotificationsCreated,
                result.Value.AlreadyNotified);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Immediate monthly product notification scan failed after reward update. reward={RewardId}.",
                reward.Id);
        }
    }
}
