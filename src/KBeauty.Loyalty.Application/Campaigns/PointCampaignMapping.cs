using KBeauty.Loyalty.Domain.Entities;

namespace KBeauty.Loyalty.Application.Campaigns;

internal static class PointCampaignMapping
{
    public static PointCampaignAdminDto ToAdminDto(this PointCampaign campaign, DateTime nowUtc) =>
        new(
            Id: campaign.Id,
            Name: campaign.Name,
            Description: campaign.Description,
            Multiplier: campaign.Multiplier,
            MinimumPurchaseAmount: campaign.MinimumPurchaseAmount,
            LevelEligibility: campaign.LevelEligibility,
            StartsAtUtc: campaign.StartsAtUtc,
            EndsAtUtc: campaign.EndsAtUtc,
            IsActive: campaign.IsActive,
            CreatedAt: campaign.CreatedAt,
            UpdatedAt: campaign.UpdatedAt,
            IsCurrentlyActive: campaign.IsCurrentlyActive(nowUtc));
}
