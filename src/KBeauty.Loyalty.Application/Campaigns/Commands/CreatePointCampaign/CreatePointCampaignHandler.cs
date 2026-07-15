using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;

namespace KBeauty.Loyalty.Application.Campaigns.Commands.CreatePointCampaign;

public sealed class CreatePointCampaignHandler : IRequestHandler<CreatePointCampaignCommand, Result<PointCampaignAdminDto>>
{
    private readonly IPointCampaignRepository _campaigns;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;

    public CreatePointCampaignHandler(IPointCampaignRepository campaigns, IDateTimeProvider dt, IUnitOfWork uow)
    {
        _campaigns = campaigns;
        _dt = dt;
        _uow = uow;
    }

    public async Task<Result<PointCampaignAdminDto>> Handle(CreatePointCampaignCommand command, CancellationToken ct)
    {
        var campaign = new PointCampaign(
            Guid.NewGuid(),
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

        return Result.Ok(campaign.ToAdminDto(_dt.UtcNow));
    }
}
