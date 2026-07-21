using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.PointCampaign;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Queries.ListPointCampaignNotificationCandidates;

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
