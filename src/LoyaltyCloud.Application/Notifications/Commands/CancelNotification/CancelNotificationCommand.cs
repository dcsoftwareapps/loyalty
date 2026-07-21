using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Commands.CancelNotification;

public sealed record CancelNotificationCommand(Guid Id) : IRequest<Result<NotificationDto>>;
