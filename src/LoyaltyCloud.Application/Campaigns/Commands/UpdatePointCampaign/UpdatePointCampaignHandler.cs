using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Campaigns.Commands.UpdatePointCampaign;

public sealed class UpdatePointCampaignHandler : IRequestHandler<UpdatePointCampaignCommand, Result<PointCampaignAdminDto>>
{
    private readonly IPointCampaignRepository _campaigns;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;

    public UpdatePointCampaignHandler(IPointCampaignRepository campaigns, IDateTimeProvider dt, IUnitOfWork uow)
    {
        _campaigns = campaigns;
        _dt = dt;
        _uow = uow;
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

        return Result.Ok(campaign.ToAdminDto(_dt.UtcNow));
    }
}
