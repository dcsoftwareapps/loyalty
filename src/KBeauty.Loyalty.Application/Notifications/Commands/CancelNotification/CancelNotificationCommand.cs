using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CancelNotification;

public sealed record CancelNotificationCommand(Guid Id) : IRequest<Result<NotificationDto>>;
