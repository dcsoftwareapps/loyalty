using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications.PointCampaign;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Queries.ListPointCampaignNotificationCandidates;

public sealed class ListPointCampaignNotificationCandidatesHandler
    : IRequestHandler<ListPointCampaignNotificationCandidatesQuery, Result<PointCampaignNotificationPreviewDto>>
{
    private readonly IPointCampaignNotificationReadService _read;

    public ListPointCampaignNotificationCandidatesHandler(IPointCampaignNotificationReadService read) => _read = read;

    public async Task<Result<PointCampaignNotificationPreviewDto>> Handle(
        ListPointCampaignNotificationCandidatesQuery query,
        CancellationToken ct)
    {
        try
        {
            var preview = await _read.ListCandidatesAsync(
                query.TimeZoneId,
                query.IncludeAlreadyNotified,
                ct);

            return Result.Ok(preview);
        }
        catch (Exception ex)
        {
            return Result.Fail<PointCampaignNotificationPreviewDto>(ex.Message);
        }
    }
}
