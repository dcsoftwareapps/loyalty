using LoyaltyCloud.Application.Notifications.MonthlyProduct;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IMonthlyProductNotificationReadService
{
    Task<MonthlyProductNotificationPreviewDto> ListCandidatesAsync(
        string timeZoneId,
        bool includeAlreadyNotified,
        Guid? rewardId = null,
        CancellationToken ct = default);
}
