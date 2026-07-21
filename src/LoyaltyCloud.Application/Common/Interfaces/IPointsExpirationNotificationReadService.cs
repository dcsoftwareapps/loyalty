using LoyaltyCloud.Application.Notifications.PointsExpiration;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IPointsExpirationNotificationReadService
{
    Task<IReadOnlyList<PointsExpirationNotificationCandidateDto>> ListCandidatesAsync(
        int daysAhead,
        string timeZoneId,
        bool includeAlreadyNotified,
        CancellationToken ct = default);
}
