using LoyaltyCloud.Application.Notifications.Custom;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ICustomNotificationAudienceReadService
{
    Task<CustomNotificationAudiencePreviewDto> PreviewAsync(
        string audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        int sampleSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<CustomNotificationAudienceRecipientDto>> ResolveRecipientsAsync(
        string audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        CancellationToken ct = default);
}
