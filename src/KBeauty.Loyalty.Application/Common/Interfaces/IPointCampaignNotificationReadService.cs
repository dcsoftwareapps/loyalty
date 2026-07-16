using KBeauty.Loyalty.Application.Notifications.PointCampaign;

namespace KBeauty.Loyalty.Application.Common.Interfaces;

public interface IPointCampaignNotificationReadService
{
    Task<PointCampaignNotificationPreviewDto> ListCandidatesAsync(
        string timeZoneId,
        bool includeAlreadyNotified,
        CancellationToken ct = default);
}
