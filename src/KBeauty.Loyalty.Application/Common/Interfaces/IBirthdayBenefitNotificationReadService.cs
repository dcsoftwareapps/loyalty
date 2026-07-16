using KBeauty.Loyalty.Application.Notifications.BirthdayBenefit;

namespace KBeauty.Loyalty.Application.Common.Interfaces;

public interface IBirthdayBenefitNotificationReadService
{
    Task<BirthdayBenefitNotificationPreviewDto> ListCandidatesAsync(
        string timeZoneId,
        bool includeAlreadyNotified,
        CancellationToken ct = default);
}
