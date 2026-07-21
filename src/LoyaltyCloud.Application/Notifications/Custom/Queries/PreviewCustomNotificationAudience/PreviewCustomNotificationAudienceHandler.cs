using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Queries.PreviewCustomNotificationAudience;

public sealed class PreviewCustomNotificationAudienceHandler
    : IRequestHandler<PreviewCustomNotificationAudienceQuery, Result<CustomNotificationAudiencePreviewDto>>
{
    private readonly ICustomNotificationAudienceReadService _audience;

    public PreviewCustomNotificationAudienceHandler(ICustomNotificationAudienceReadService audience) => _audience = audience;

    public async Task<Result<CustomNotificationAudiencePreviewDto>> Handle(
        PreviewCustomNotificationAudienceQuery query,
        CancellationToken ct)
    {
        try
        {
            var preview = await _audience.PreviewAsync(
                query.AudienceType,
                query.MinimumPoints,
                query.PointsExpiringDaysAhead,
                query.SampleSize,
                ct);
            return Result.Ok(preview);
        }
        catch (Exception ex)
        {
            return Result.Fail<CustomNotificationAudiencePreviewDto>(ex.Message);
        }
    }
}
