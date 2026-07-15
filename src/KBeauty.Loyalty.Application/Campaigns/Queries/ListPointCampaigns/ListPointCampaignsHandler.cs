using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;

namespace KBeauty.Loyalty.Application.Campaigns.Queries.ListPointCampaigns;

public sealed class ListPointCampaignsHandler
    : IRequestHandler<ListPointCampaignsQuery, Result<IReadOnlyList<PointCampaignAdminDto>>>
{
    private readonly IPointCampaignRepository _campaigns;
    private readonly IDateTimeProvider _dt;

    public ListPointCampaignsHandler(IPointCampaignRepository campaigns, IDateTimeProvider dt)
    {
        _campaigns = campaigns;
        _dt = dt;
    }

    public async Task<Result<IReadOnlyList<PointCampaignAdminDto>>> Handle(ListPointCampaignsQuery query, CancellationToken ct)
    {
        var now = _dt.UtcNow;
        var campaigns = await _campaigns.GetAllAsync(ct);
        return Result.Ok<IReadOnlyList<PointCampaignAdminDto>>(campaigns.Select(c => c.ToAdminDto(now)).ToList());
    }
}
