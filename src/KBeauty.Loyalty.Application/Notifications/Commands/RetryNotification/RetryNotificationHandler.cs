using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.RetryNotification;

public sealed class RetryNotificationHandler : IRequestHandler<RetryNotificationCommand, Result<NotificationDto>>
{
    private readonly ILoyaltyNotificationService _service;

    public RetryNotificationHandler(ILoyaltyNotificationService service) => _service = service;

    public async Task<Result<NotificationDto>> Handle(RetryNotificationCommand command, CancellationToken ct)
    {
        try { return Result.Ok(await _service.RetryAsync(command.Id, ct)); }
        catch (Exception ex) { return Result.Fail<NotificationDto>(ex.Message); }
    }
}
