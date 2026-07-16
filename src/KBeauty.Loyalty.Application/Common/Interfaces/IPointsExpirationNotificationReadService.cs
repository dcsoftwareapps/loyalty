using KBeauty.Loyalty.Application.Notifications.PointsExpiration;

namespace KBeauty.Loyalty.Application.Common.Interfaces;

public interface IPointsExpirationNotificationReadService
{
    Task<IReadOnlyList<PointsExpirationNotificationCandidateDto>> ListCandidatesAsync(
        int daysAhead,
        string timeZoneId,
        bool includeAlreadyNotified,
        CancellationToken ct = default);
}
