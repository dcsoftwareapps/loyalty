using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Commands.CancelCustomNotificationCampaign;

public sealed record CancelCustomNotificationCampaignCommand(Guid CampaignId)
    : IRequest<Result<CustomNotificationCampaignDto>>;
