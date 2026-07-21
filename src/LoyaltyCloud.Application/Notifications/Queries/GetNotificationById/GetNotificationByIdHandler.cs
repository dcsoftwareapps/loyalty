using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Queries.GetNotificationById;

public sealed class GetNotificationByIdHandler : IRequestHandler<GetNotificationByIdQuery, Result<NotificationDto>>
{
    private readonly ILoyaltyNotificationService _service;

    public GetNotificationByIdHandler(ILoyaltyNotificationService service) => _service = service;

    public async Task<Result<NotificationDto>> Handle(GetNotificationByIdQuery query, CancellationToken ct)
    {
        var dto = await _service.GetByIdAsync(query.Id, ct);
        return dto is null
            ? Result.Fail<NotificationDto>($"No se encontro notificacion {query.Id}.")
            : Result.Ok(dto);
    }
}
