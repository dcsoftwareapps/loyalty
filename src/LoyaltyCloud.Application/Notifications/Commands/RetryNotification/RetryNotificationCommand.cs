using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Commands.RetryNotification;

public sealed record RetryNotificationCommand(Guid Id) : IRequest<Result<NotificationDto>>;
