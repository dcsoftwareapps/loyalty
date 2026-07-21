using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Queries.GetCustomNotificationCampaignById;

public sealed class GetCustomNotificationCampaignByIdHandler
    : IRequestHandler<GetCustomNotificationCampaignByIdQuery, Result<CustomNotificationCampaignDto>>
{
    private readonly ICustomNotificationCampaignRepository _campaigns;

    public GetCustomNotificationCampaignByIdHandler(ICustomNotificationCampaignRepository campaigns) => _campaigns = campaigns;

    public async Task<Result<CustomNotificationCampaignDto>> Handle(GetCustomNotificationCampaignByIdQuery query, CancellationToken ct)
    {
        try
        {
            var campaign = await _campaigns.GetByIdAsync(query.CampaignId, ct);
            return campaign is null
                ? Result.Fail<CustomNotificationCampaignDto>("Campana personalizada no encontrada.")
                : Result.Ok(CustomNotificationCampaignMapper.ToDto(campaign));
        }
        catch (Exception ex)
        {
            return Result.Fail<CustomNotificationCampaignDto>(ex.Message);
        }
    }
}
