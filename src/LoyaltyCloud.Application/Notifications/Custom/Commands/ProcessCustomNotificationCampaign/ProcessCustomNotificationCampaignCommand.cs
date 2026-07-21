using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Commands.ProcessCustomNotificationCampaign;

public sealed record ProcessCustomNotificationCampaignCommand(Guid CampaignId)
    : IRequest<Result<CustomNotificationCampaignProcessingDto>>;
