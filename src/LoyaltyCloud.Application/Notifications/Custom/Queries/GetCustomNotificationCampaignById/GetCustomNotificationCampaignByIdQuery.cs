using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Queries.GetCustomNotificationCampaignById;

public sealed record GetCustomNotificationCampaignByIdQuery(Guid CampaignId)
    : IRequest<Result<CustomNotificationCampaignDto>>;
