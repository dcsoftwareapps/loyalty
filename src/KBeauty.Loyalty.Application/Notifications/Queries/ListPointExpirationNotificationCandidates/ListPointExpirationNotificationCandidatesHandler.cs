using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications.PointsExpiration;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Queries.ListPointExpirationNotificationCandidates;

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
