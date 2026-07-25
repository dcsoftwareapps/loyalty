using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.Commands.CreateMonthlyProductStartedNotifications;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Rewards.Commands.CreateReward;

public sealed class CreateRewardHandler : IRequestHandler<CreateRewardCommand, Result<RewardAdminDto>>
{
    private readonly IRewardCatalogRepository _rewards;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantLoyaltyLevelReadService _tenantLevels;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ISender _sender;
    private readonly ILogger<CreateRewardHandler> _logger;

    public CreateRewardHandler(
        IRewardCatalogRepository rewards,
        ITenantContext tenantContext,
        ITenantLoyaltyLevelReadService tenantLevels,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ISender sender,
        ILogger<CreateRewardHandler> logger)
    {
        _rewards = rewards;
        _tenantContext = tenantContext;
        _tenantLevels = tenantLevels;
        _dt = dt;
        _uow = uow;
        _sender = sender;
        _logger = logger;
    }

    public async Task<Result<RewardAdminDto>> Handle(CreateRewardCommand command, CancellationToken ct)
    {
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
                excludeRewardId: null,
                ct);
            if (overlaps)
                return Result.Fail<RewardAdminDto>(
                    "Ya existe un Producto del mes activo con una vigencia que se traslapa.");
        }

        var reward = new RewardCatalogItem(
            id: Guid.NewGuid(),
            tenantId: _tenantContext.RequireTenantId(),
            name: command.Name,
            description: command.Description,
            pointsCost: command.PointsCost,
            minLevel: canonicalMinLevel,
            isMonthlyProduct: command.IsMonthlyProduct,
            validFrom: command.ValidFrom,
            validTo: command.ValidTo);

        if (!command.IsActive)
            reward.Deactivate();

        await _rewards.AddAsync(reward, ct);
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
                    "Immediate monthly product notification scan failed after reward create. reward={RewardId}, error={Error}.",
                    reward.Id,
                    result.Error);
                return;
            }

            _logger.LogInformation(
                "Directed MonthlyProduct notification generation completed after reward create. RewardId={RewardId}, EligibleCards={EligibleCards}, Created={Created}, AlreadyNotified={AlreadyNotified}.",
                reward.Id,
                result.Value.CardsEligible,
                result.Value.NotificationsCreated,
                result.Value.AlreadyNotified);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Immediate monthly product notification scan failed after reward create. reward={RewardId}.",
                reward.Id);
        }
    }
}
