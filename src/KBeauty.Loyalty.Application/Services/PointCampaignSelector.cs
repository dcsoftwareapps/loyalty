using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Domain.Repositories;

namespace KBeauty.Loyalty.Application.Services;

internal sealed class PointCampaignSelector : IPointCampaignSelector
{
    private readonly IPointCampaignRepository _campaigns;

    public PointCampaignSelector(IPointCampaignRepository campaigns) => _campaigns = campaigns;

    public async Task<SelectedPointCampaign?> SelectBestAsync(
        DateTime nowUtc,
        decimal purchaseAmount,
        string loyaltyLevel,
        CancellationToken ct = default)
    {
        var campaign = await _campaigns.GetBestApplicableAsync(nowUtc, purchaseAmount, loyaltyLevel, ct);
        return campaign is null
            ? null
            : new SelectedPointCampaign(campaign.Id, campaign.Name, campaign.Multiplier);
    }
}
