using KBeauty.Loyalty.Application.Notifications.Custom;
using KBeauty.Loyalty.Application.Notifications.Custom.Commands.ProcessCustomNotificationCampaign;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Custom.Commands.SendCustomNotificationCampaign;

public sealed class SendCustomNotificationCampaignHandler
    : IRequestHandler<SendCustomNotificationCampaignCommand, Result<CustomNotificationCampaignProcessingDto>>
{
    private readonly ISender _sender;

    public SendCustomNotificationCampaignHandler(ISender sender) => _sender = sender;

    public async Task<Result<CustomNotificationCampaignProcessingDto>> Handle(
        SendCustomNotificationCampaignCommand command,
        CancellationToken ct) =>
        await _sender.Send(new ProcessCustomNotificationCampaignCommand(command.CampaignId), ct);
}
