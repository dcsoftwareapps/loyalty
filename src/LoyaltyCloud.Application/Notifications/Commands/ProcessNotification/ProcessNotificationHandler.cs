using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Commands.ProcessNotification;

public sealed class ProcessNotificationHandler : IRequestHandler<ProcessNotificationCommand, Result<NotificationDto>>
{
    private readonly ILoyaltyNotificationService _service;

    public ProcessNotificationHandler(ILoyaltyNotificationService service) => _service = service;

    public async Task<Result<NotificationDto>> Handle(ProcessNotificationCommand command, CancellationToken ct)
    {
        try { return Result.Ok(await _service.ProcessAsync(command.Id, ct)); }
        catch (Exception ex) { return Result.Fail<NotificationDto>(ex.Message); }
    }
}
