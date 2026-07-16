using KBeauty.Loyalty.Application.Notifications.MonthlyProduct;

namespace KBeauty.Loyalty.Application.Common.Interfaces;

public interface IMonthlyProductNotificationReadService
{
    Task<MonthlyProductNotificationPreviewDto> ListCandidatesAsync(
        string timeZoneId,
        bool includeAlreadyNotified,
        CancellationToken ct = default);
}
