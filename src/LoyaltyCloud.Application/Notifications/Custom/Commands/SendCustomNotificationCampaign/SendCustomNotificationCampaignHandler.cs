using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Application.Notifications.Custom.Commands.ProcessCustomNotificationCampaign;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Commands.SendCustomNotificationCampaign;

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
