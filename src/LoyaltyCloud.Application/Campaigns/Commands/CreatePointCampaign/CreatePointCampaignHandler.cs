using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Campaigns.Commands.CreatePointCampaign;

public sealed class CreatePointCampaignHandler : IRequestHandler<CreatePointCampaignCommand, Result<PointCampaignAdminDto>>
{
    private readonly IPointCampaignRepository _campaigns;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;

    public CreatePointCampaignHandler(
        IPointCampaignRepository campaigns,
        ITenantContext tenantContext,
        IDateTimeProvider dt,
        IUnitOfWork uow)
    {
        _campaigns = campaigns;
        _tenantContext = tenantContext;
        _dt = dt;
        _uow = uow;
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

        return Result.Ok(campaign.ToAdminDto(_dt.UtcNow));
    }
}
