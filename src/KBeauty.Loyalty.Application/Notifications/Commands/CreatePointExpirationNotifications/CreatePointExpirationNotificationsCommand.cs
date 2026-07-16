using KBeauty.Loyalty.Application.Notifications.PointsExpiration;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CreatePointExpirationNotifications;

public sealed record CreatePointExpirationNotificationsCommand(
    string OperatorId,
    int DaysAhead = 15,
    string TimeZoneId = "America/Tijuana") : IRequest<Result<CreatePointExpirationNotificationsResponse>>;
