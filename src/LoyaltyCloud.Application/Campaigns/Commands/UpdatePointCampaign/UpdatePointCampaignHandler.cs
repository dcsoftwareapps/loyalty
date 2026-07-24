using LoyaltyCloud.Application.Notifications.Commands.CreatePointCampaignStartedNotifications;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Campaigns.Commands.UpdatePointCampaign;

public sealed class UpdatePointCampaignHandler : IRequestHandler<UpdatePointCampaignCommand, Result<PointCampaignAdminDto>>
{
    private readonly IPointCampaignRepository _campaigns;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ISender _sender;
    private readonly ILogger<UpdatePointCampaignHandler> _logger;

    public UpdatePointCampaignHandler(
        IPointCampaignRepository campaigns,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ISender sender,
        ILogger<UpdatePointCampaignHandler> logger)
    {
        _campaigns = campaigns;
        _dt = dt;
        _uow = uow;
        _sender = sender;
        _logger = logger;
    }

    public async Task<Result<PointCampaignAdminDto>> Handle(UpdatePointCampaignCommand command, CancellationToken ct)
    {
        var campaign = await _campaigns.GetByIdAsync(command.Id, ct);
        if (campaign is null)
            return Result.Fail<PointCampaignAdminDto>($"No se encontro campana con id '{command.Id}'.");

        campaign.Update(
            command.Name,
            command.Description,
            command.Multiplier,
            command.MinimumPurchaseAmount,
            command.LevelEligibility,
            command.StartsAtUtc,
            command.EndsAtUtc,
            _dt.UtcNow);

        if (command.IsActive)
            campaign.Activate(_dt.UtcNow);
        else
            campaign.Deactivate(_dt.UtcNow);

        _campaigns.Update(campaign);
        await _uow.SaveChangesAsync(ct);

        await TriggerPointCampaignNotificationsIfActiveAsync(campaign, ct);

        return Result.Ok(campaign.ToAdminDto(_dt.UtcNow));
    }

    private async Task TriggerPointCampaignNotificationsIfActiveAsync(PointCampaign campaign, CancellationToken ct)
    {
        if (!campaign.IsCurrentlyActive(_dt.UtcNow))
            return;

        try
        {
            var result = await _sender.Send(new CreatePointCampaignStartedNotificationsCommand("campaign-admin"), ct);
            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Immediate point campaign notification scan failed after campaign update. campaign={CampaignId}, error={Error}.",
                    campaign.Id,
                    result.Error);
                return;
            }

            _logger.LogInformation(
                "Immediate point campaign notification scan completed after campaign update. campaign={CampaignId}, created={Created}, alreadyNotified={AlreadyNotified}.",
                campaign.Id,
                result.Value.NotificationsCreated,
                result.Value.AlreadyNotified);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Immediate point campaign notification scan failed after campaign update. campaign={CampaignId}.",
                campaign.Id);
        }
    }
}
