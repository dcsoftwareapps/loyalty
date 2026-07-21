using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Queries.ListNotifications;

public sealed class ListNotificationsHandler
    : IRequestHandler<ListNotificationsQuery, Result<IReadOnlyList<NotificationDto>>>
{
    private readonly ILoyaltyNotificationService _service;

    public ListNotificationsHandler(ILoyaltyNotificationService service) => _service = service;

    public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(ListNotificationsQuery query, CancellationToken ct) =>
        Result.Ok(await _service.ListAsync(query.CustomerId, query.Type, query.Status, query.Channel, query.FromUtc, query.ToUtc, query.Take, ct));
}
