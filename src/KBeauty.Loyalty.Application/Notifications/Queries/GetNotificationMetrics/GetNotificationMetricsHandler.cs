using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Queries.GetNotificationMetrics;

public sealed class GetNotificationMetricsHandler : IRequestHandler<GetNotificationMetricsQuery, Result<NotificationMetricsDto>>
{
    private readonly ILoyaltyNotificationService _service;

    public GetNotificationMetricsHandler(ILoyaltyNotificationService service) => _service = service;

    public async Task<Result<NotificationMetricsDto>> Handle(GetNotificationMetricsQuery query, CancellationToken ct) =>
        Result.Ok(await _service.GetMetricsAsync(ct));
}
