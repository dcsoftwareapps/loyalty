using LoyaltyCloud.Application.Notifications.BirthdayBenefit;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IBirthdayBenefitNotificationReadService
{
    Task<BirthdayBenefitNotificationPreviewDto> ListCandidatesAsync(
        string timeZoneId,
        bool includeAlreadyNotified,
        CancellationToken ct = default);
}
