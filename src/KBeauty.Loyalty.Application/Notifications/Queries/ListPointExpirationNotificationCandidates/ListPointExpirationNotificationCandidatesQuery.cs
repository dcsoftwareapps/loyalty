using KBeauty.Loyalty.Application.Notifications.PointsExpiration;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Queries.ListPointExpirationNotificationCandidates;

public sealed record ListPointExpirationNotificationCandidatesQuery(
    int DaysAhead = 15,
    string TimeZoneId = "America/Tijuana",
    bool IncludeAlreadyNotified = false) : IRequest<Result<IReadOnlyList<PointsExpirationNotificationCandidateDto>>>;
