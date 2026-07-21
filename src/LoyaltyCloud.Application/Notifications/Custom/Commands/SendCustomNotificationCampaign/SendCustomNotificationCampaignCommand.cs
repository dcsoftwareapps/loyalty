using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Commands.SendCustomNotificationCampaign;

public sealed record SendCustomNotificationCampaignCommand(Guid CampaignId)
    : IRequest<Result<CustomNotificationCampaignProcessingDto>>;
