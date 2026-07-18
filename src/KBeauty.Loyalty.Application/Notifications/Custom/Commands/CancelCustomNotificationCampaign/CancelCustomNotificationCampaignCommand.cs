using KBeauty.Loyalty.Application.Notifications.Custom;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Custom.Commands.CancelCustomNotificationCampaign;

public sealed record CancelCustomNotificationCampaignCommand(Guid CampaignId)
    : IRequest<Result<CustomNotificationCampaignDto>>;
