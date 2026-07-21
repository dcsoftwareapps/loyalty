using LoyaltyCloud.Application.Notifications.PointCampaign;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IPointCampaignNotificationReadService
{
    Task<PointCampaignNotificationPreviewDto> ListCandidatesAsync(
        string timeZoneId,
        bool includeAlreadyNotified,
        CancellationToken ct = default);
}
