using LoyaltyCloud.Application.Notifications.BirthdayBenefit;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Commands.CreateBirthdayBenefitStartedNotifications;

public sealed record CreateBirthdayBenefitStartedNotificationsCommand(
    string OperatorId,
    string TimeZoneId = "America/Tijuana") : IRequest<Result<CreateBirthdayBenefitStartedNotificationsResponse>>;
