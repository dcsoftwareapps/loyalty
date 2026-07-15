using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.RetryNotification;

public sealed record RetryNotificationCommand(Guid Id) : IRequest<Result<NotificationDto>>;
