using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.ProcessNotification;

public sealed record ProcessNotificationCommand(Guid Id) : IRequest<Result<NotificationDto>>;
