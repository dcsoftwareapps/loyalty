using KBeauty.Loyalty.Application.Notifications.BirthdayBenefit;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CreateBirthdayBenefitStartedNotifications;

public sealed record CreateBirthdayBenefitStartedNotificationsCommand(
    string OperatorId,
    string TimeZoneId = "America/Tijuana") : IRequest<Result<CreateBirthdayBenefitStartedNotificationsResponse>>;
