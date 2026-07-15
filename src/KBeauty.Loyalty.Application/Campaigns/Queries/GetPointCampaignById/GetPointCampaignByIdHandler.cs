using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;

namespace KBeauty.Loyalty.Application.Campaigns.Queries.GetPointCampaignById;

public sealed class GetPointCampaignByIdHandler : IRequestHandler<GetPointCampaignByIdQuery, Result<PointCampaignAdminDto>>
{
    private readonly IPointCampaignRepository _campaigns;
    private readonly IDateTimeProvider _dt;

    public GetPointCampaignByIdHandler(IPointCampaignRepository campaigns, IDateTimeProvider dt)
    {
        _campaigns = campaigns;
        _dt = dt;
    }

    public async Task<Result<PointCampaignAdminDto>> Handle(GetPointCampaignByIdQuery query, CancellationToken ct)
    {
        var campaign = await _campaigns.GetByIdAsync(query.Id, ct);
        return campaign is null
            ? Result.Fail<PointCampaignAdminDto>($"No se encontro campana con id '{query.Id}'.")
            : Result.Ok(campaign.ToAdminDto(_dt.UtcNow));
    }
}
