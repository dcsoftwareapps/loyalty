using LoyaltyCloud.Application.Notifications.PointsExpiration;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Queries.ListPointExpirationNotificationCandidates;

public sealed record ListPointExpirationNotificationCandidatesQuery(
    int DaysAhead = 15,
    string TimeZoneId = "America/Tijuana",
    bool IncludeAlreadyNotified = false) : IRequest<Result<IReadOnlyList<PointsExpirationNotificationCandidateDto>>>;
