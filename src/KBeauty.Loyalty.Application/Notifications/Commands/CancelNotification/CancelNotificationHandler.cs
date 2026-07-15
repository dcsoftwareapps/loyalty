using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CancelNotification;

public sealed class CancelNotificationHandler : IRequestHandler<CancelNotificationCommand, Result<NotificationDto>>
{
    private readonly ILoyaltyNotificationService _service;

    public CancelNotificationHandler(ILoyaltyNotificationService service) => _service = service;

    public async Task<Result<NotificationDto>> Handle(CancelNotificationCommand command, CancellationToken ct)
    {
        try { return Result.Ok(await _service.CancelAsync(command.Id, ct)); }
        catch (Exception ex) { return Result.Fail<NotificationDto>(ex.Message); }
    }
}
