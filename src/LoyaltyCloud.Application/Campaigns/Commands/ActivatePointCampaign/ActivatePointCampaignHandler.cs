using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Campaigns.Commands.ActivatePointCampaign;

public sealed class ActivatePointCampaignHandler : IRequestHandler<ActivatePointCampaignCommand, Result<PointCampaignAdminDto>>
{
    private readonly IPointCampaignRepository _campaigns;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;

    public ActivatePointCampaignHandler(IPointCampaignRepository campaigns, IDateTimeProvider dt, IUnitOfWork uow)
    {
        _campaigns = campaigns;
        _dt = dt;
        _uow = uow;
    }

    public async Task<Result<PointCampaignAdminDto>> Handle(ActivatePointCampaignCommand command, CancellationToken ct)
    {
        var campaign = await _campaigns.GetByIdAsync(command.Id, ct);
        if (campaign is null)
            return Result.Fail<PointCampaignAdminDto>($"No se encontro campana con id '{command.Id}'.");

        campaign.Activate(_dt.UtcNow);
        _campaigns.Update(campaign);
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(campaign.ToAdminDto(_dt.UtcNow));
    }
}
