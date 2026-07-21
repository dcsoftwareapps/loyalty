using LoyaltyCloud.Application.Notifications.MonthlyProduct;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Commands.CreateMonthlyProductStartedNotifications;

public sealed record CreateMonthlyProductStartedNotificationsCommand(
    string OperatorId,
    string TimeZoneId = "America/Tijuana") : IRequest<Result<CreateMonthlyProductStartedNotificationsResponse>>;
