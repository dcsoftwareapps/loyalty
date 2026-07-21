using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Commands.ProcessNotification;

public sealed record ProcessNotificationCommand(Guid Id) : IRequest<Result<NotificationDto>>;
