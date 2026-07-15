using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CreateNotification;

public sealed class CreateNotificationHandler : IRequestHandler<CreateNotificationCommand, Result<NotificationDto>>
{
    private readonly ILoyaltyNotificationService _service;

    public CreateNotificationHandler(ILoyaltyNotificationService service) => _service = service;

    public async Task<Result<NotificationDto>> Handle(CreateNotificationCommand command, CancellationToken ct)
    {
        try
        {
            var dto = await _service.CreateAsync(new CreateLoyaltyNotificationRequest(
                command.SerialNumber,
                command.Type,
                command.Title,
                command.Message,
                command.ScheduledAtUtc,
                command.DisplayUntilUtc,
                command.Channels,
                command.CorrelationId,
                command.Source,
                command.MetadataJson,
                command.ProcessImmediately), ct);
            return Result.Ok(dto);
        }
        catch (Exception ex)
        {
            return Result.Fail<NotificationDto>(ex.Message);
        }
    }
}
