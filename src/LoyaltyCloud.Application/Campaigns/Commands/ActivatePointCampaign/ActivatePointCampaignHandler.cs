using LoyaltyCloud.Application.Notifications.Commands.CreatePointCampaignStartedNotifications;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Campaigns.Commands.ActivatePointCampaign;

public sealed class ActivatePointCampaignHandler : IRequestHandler<ActivatePointCampaignCommand, Result<PointCampaignAdminDto>>
{
    private readonly IPointCampaignRepository _campaigns;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ISender _sender;
    private readonly ILogger<ActivatePointCampaignHandler> _logger;

    public ActivatePointCampaignHandler(
        IPointCampaignRepository campaigns,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ISender sender,
        ILogger<ActivatePointCampaignHandler> logger)
    {
        _campaigns = campaigns;
        _dt = dt;
        _uow = uow;
        _sender = sender;
        _logger = logger;
    }

    public async Task<Result<PointCampaignAdminDto>> Handle(ActivatePointCampaignCommand command, CancellationToken ct)
    {
        var campaign = await _campaigns.GetByIdAsync(command.Id, ct);
        if (campaign is null)
            return Result.Fail<PointCampaignAdminDto>($"No se encontro campana con id '{command.Id}'.");

        campaign.Activate(_dt.UtcNow);
        _campaigns.Update(campaign);
        await _uow.SaveChangesAsync(ct);

        await TriggerPointCampaignNotificationsIfActiveAsync(campaign, ct);

        return Result.Ok(campaign.ToAdminDto(_dt.UtcNow));
    }

    private async Task TriggerPointCampaignNotificationsIfActiveAsync(Domain.Entities.PointCampaign campaign, CancellationToken ct)
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
                    "Immediate point campaign notification scan failed after campaign activate. campaign={CampaignId}, error={Error}.",
                    campaign.Id,
                    result.Error);
                return;
            }

            _logger.LogInformation(
                "Directed PointCampaign notification generation completed after campaign activate. CampaignId={CampaignId}, EligibleCards={EligibleCards}, Created={Created}, AlreadyNotified={AlreadyNotified}.",
                campaign.Id,
                result.Value.CardsEligible,
                result.Value.NotificationsCreated,
                result.Value.AlreadyNotified);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Immediate point campaign notification scan failed after campaign activate. campaign={CampaignId}.",
                campaign.Id);
        }
    }
}
