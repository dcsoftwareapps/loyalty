using KBeauty.Loyalty.Application.Notifications.Custom;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Custom.Queries.GetCustomNotificationCampaignById;

public sealed record GetCustomNotificationCampaignByIdQuery(Guid CampaignId)
    : IRequest<Result<CustomNotificationCampaignDto>>;
