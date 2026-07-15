using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.ProcessPendingNotifications;

public sealed class ProcessPendingNotificationsHandler : IRequestHandler<ProcessPendingNotificationsCommand, Result<int>>
{
    private readonly ILoyaltyNotificationService _service;

    public ProcessPendingNotificationsHandler(ILoyaltyNotificationService service) => _service = service;

    public async Task<Result<int>> Handle(ProcessPendingNotificationsCommand command, CancellationToken ct)
    {
        try
        {
            var processed = await _service.ProcessPendingAsync(command.BatchSize, command.MaxAttempts, ct);
            return Result.Ok(processed);
        }
        catch (Exception ex)
        {
            return Result.Fail<int>(ex.Message);
        }
    }
}
