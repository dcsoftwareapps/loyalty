using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Queries.GetNotificationById;

public sealed record GetNotificationByIdQuery(Guid Id) : IRequest<Result<NotificationDto>>;
