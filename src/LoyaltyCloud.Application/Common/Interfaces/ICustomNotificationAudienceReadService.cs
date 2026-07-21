using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ICustomNotificationAudienceReadService
{
    Task<CustomNotificationAudiencePreviewDto> PreviewAsync(
        CustomNotificationAudienceType audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        int sampleSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<CustomNotificationAudienceRecipientDto>> ResolveRecipientsAsync(
        CustomNotificationAudienceType audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        CancellationToken ct = default);
}
