using KBeauty.Loyalty.Application.Notifications.Custom;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Custom.Commands.SendCustomNotificationCampaign;

public sealed record SendCustomNotificationCampaignCommand(Guid CampaignId)
    : IRequest<Result<CustomNotificationCampaignProcessingDto>>;
