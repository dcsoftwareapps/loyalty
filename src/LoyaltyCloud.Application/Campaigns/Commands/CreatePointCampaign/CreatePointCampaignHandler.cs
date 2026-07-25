using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.Commands.CreatePointCampaignStartedNotifications;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Campaigns.Commands.CreatePointCampaign;

public sealed class CreatePointCampaignHandler : IRequestHandler<CreatePointCampaignCommand, Result<PointCampaignAdminDto>>
{
    private readonly IPointCampaignRepository _campaigns;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ISender _sender;
    private readonly ILogger<CreatePointCampaignHandler> _logger;

    public CreatePointCampaignHandler(
        IPointCampaignRepository campaigns,
        ITenantContext tenantContext,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ISender sender,
        ILogger<CreatePointCampaignHandler> logger)
    {
        _campaigns = campaigns;
        _tenantContext = tenantContext;
        _dt = dt;
        _uow = uow;
        _sender = sender;
        _logger = logger;
    }

    public async Task<Result<PointCampaignAdminDto>> Handle(CreatePointCampaignCommand command, CancellationToken ct)
    {
        var campaign = new PointCampaign(
            Guid.NewGuid(),
            _tenantContext.RequireTenantId(),
            command.Name,
            command.Description,
            command.Multiplier,
            command.MinimumPurchaseAmount,
            command.LevelEligibility,
            command.StartsAtUtc,
            command.EndsAtUtc,
            _dt.UtcNow);

        if (!command.IsActive)
            campaign.Deactivate(_dt.UtcNow);

        await _campaigns.AddAsync(campaign, ct);
        await _uow.SaveChangesAsync(ct);

        await TriggerPointCampaignNotificationsIfActiveAsync(campaign, ct);

        return Result.Ok(campaign.ToAdminDto(_dt.UtcNow));
    }

    private async Task TriggerPointCampaignNotificationsIfActiveAsync(PointCampaign campaign, CancellationToken ct)
    {
        var nowUtc = _dt.UtcNow;
        var isCurrentlyActive = campaign.IsCurrentlyActive(nowUtc);
        _logger.LogInformation(
            "Immediate PointCampaign notification trigger. Tenant={TenantId}, CampaignId={CampaignId}, CampaignName={CampaignName}, IsActive={IsActive}, StartsAtUtc={StartsAtUtc:O}, EndsAtUtc={EndsAtUtc:O}, NowUtc={NowUtc:O}, IsCurrentlyActive={IsCurrentlyActive}.",
            campaign.TenantId,
            campaign.Id,
            campaign.Name,
            campaign.IsActive,
            campaign.StartsAtUtc,
            campaign.EndsAtUtc,
            nowUtc,
            isCurrentlyActive);

        if (!isCurrentlyActive)
            return;

        try
        {
            var result = await _sender.Send(new CreatePointCampaignStartedNotificationsCommand("campaign-admin", CampaignId: campaign.Id), ct);
            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Immediate point campaign notification scan failed after campaign create. campaign={CampaignId}, error={Error}.",
                    campaign.Id,
                    result.Error);
                return;
            }

            _logger.LogInformation(
                "Directed PointCampaign notification generation completed after campaign create. CampaignId={CampaignId}, EligibleCards={EligibleCards}, Created={Created}, AlreadyNotified={AlreadyNotified}.",
                campaign.Id,
                result.Value.CardsEligible,
                result.Value.NotificationsCreated,
                result.Value.AlreadyNotified);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Immediate point campaign notification scan failed after campaign create. campaign={CampaignId}.",
                campaign.Id);
        }
    }
}
