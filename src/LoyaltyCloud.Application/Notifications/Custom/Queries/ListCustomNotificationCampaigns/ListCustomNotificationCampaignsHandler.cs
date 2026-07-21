using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Queries.ListCustomNotificationCampaigns;

public sealed class ListCustomNotificationCampaignsHandler
    : IRequestHandler<ListCustomNotificationCampaignsQuery, Result<IReadOnlyList<CustomNotificationCampaignDto>>>
{
    private readonly ICustomNotificationCampaignRepository _campaigns;

    public ListCustomNotificationCampaignsHandler(ICustomNotificationCampaignRepository campaigns) => _campaigns = campaigns;

    public async Task<Result<IReadOnlyList<CustomNotificationCampaignDto>>> Handle(
        ListCustomNotificationCampaignsQuery query,
        CancellationToken ct)
    {
        try
        {
            var rows = await _campaigns.ListAsync(query.Status, query.Take, ct);
            return Result.Ok<IReadOnlyList<CustomNotificationCampaignDto>>(rows.Select(CustomNotificationCampaignMapper.ToDto).ToList());
        }
        catch (Exception ex)
        {
            return Result.Fail<IReadOnlyList<CustomNotificationCampaignDto>>(ex.Message);
        }
    }
}
