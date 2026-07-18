using KBeauty.Loyalty.Application.Notifications.Custom;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Application.Notifications.Custom.Commands.CancelCustomNotificationCampaign;

public sealed class CancelCustomNotificationCampaignHandler
    : IRequestHandler<CancelCustomNotificationCampaignCommand, Result<CustomNotificationCampaignDto>>
{
    private readonly ICustomNotificationCampaignRepository _campaigns;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _dt;
    private readonly ILogger<CancelCustomNotificationCampaignHandler> _logger;

    public CancelCustomNotificationCampaignHandler(
        ICustomNotificationCampaignRepository campaigns,
        IUnitOfWork uow,
        IDateTimeProvider dt,
        ILogger<CancelCustomNotificationCampaignHandler> logger)
    {
        _campaigns = campaigns;
        _uow = uow;
        _dt = dt;
        _logger = logger;
    }

    public async Task<Result<CustomNotificationCampaignDto>> Handle(CancelCustomNotificationCampaignCommand command, CancellationToken ct)
    {
        try
        {
            var campaign = await _campaigns.GetByIdAsync(command.CampaignId, ct);
            if (campaign is null)
                return Result.Fail<CustomNotificationCampaignDto>("Campana personalizada no encontrada.");

            campaign.Cancel(_dt.UtcNow);
            _campaigns.Update(campaign);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Custom notification campaign {CampaignId} cancelled.", campaign.Id);
            return Result.Ok(CustomNotificationCampaignMapper.ToDto(campaign));
        }
        catch (Exception ex)
        {
            return Result.Fail<CustomNotificationCampaignDto>(ex.Message);
        }
    }
}
