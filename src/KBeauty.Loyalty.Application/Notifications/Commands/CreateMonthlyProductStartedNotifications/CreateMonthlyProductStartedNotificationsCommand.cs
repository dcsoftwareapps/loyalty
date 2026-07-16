using KBeauty.Loyalty.Application.Notifications.MonthlyProduct;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CreateMonthlyProductStartedNotifications;

public sealed record CreateMonthlyProductStartedNotificationsCommand(
    string OperatorId,
    string TimeZoneId = "America/Tijuana") : IRequest<Result<CreateMonthlyProductStartedNotificationsResponse>>;
