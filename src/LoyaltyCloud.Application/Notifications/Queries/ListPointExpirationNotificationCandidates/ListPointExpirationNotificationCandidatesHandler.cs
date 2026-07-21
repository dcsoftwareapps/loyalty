using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.PointsExpiration;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Queries.ListPointExpirationNotificationCandidates;

public sealed class ListPointExpirationNotificationCandidatesHandler
    : IRequestHandler<ListPointExpirationNotificationCandidatesQuery, Result<IReadOnlyList<PointsExpirationNotificationCandidateDto>>>
{
    private readonly IPointsExpirationNotificationReadService _read;

    public ListPointExpirationNotificationCandidatesHandler(IPointsExpirationNotificationReadService read) => _read = read;

    public async Task<Result<IReadOnlyList<PointsExpirationNotificationCandidateDto>>> Handle(
        ListPointExpirationNotificationCandidatesQuery query,
        CancellationToken ct)
    {
        try
        {
            var rows = await _read.ListCandidatesAsync(
                query.DaysAhead,
                query.TimeZoneId,
                query.IncludeAlreadyNotified,
                ct);

            return Result.Ok(rows);
        }
        catch (Exception ex)
        {
            return Result.Fail<IReadOnlyList<PointsExpirationNotificationCandidateDto>>(ex.Message);
        }
    }
}
