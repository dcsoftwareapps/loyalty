using KBeauty.Loyalty.Application.Notifications.Custom;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Custom.Commands.ProcessCustomNotificationCampaign;

public sealed record ProcessCustomNotificationCampaignCommand(Guid CampaignId)
    : IRequest<Result<CustomNotificationCampaignProcessingDto>>;
