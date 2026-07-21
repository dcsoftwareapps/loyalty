using LoyaltyCloud.Application.Notifications.PointsExpiration;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Commands.CreatePointExpirationNotifications;

public sealed record CreatePointExpirationNotificationsCommand(
    string OperatorId,
    int DaysAhead = 15,
    string TimeZoneId = "America/Tijuana") : IRequest<Result<CreatePointExpirationNotificationsResponse>>;
