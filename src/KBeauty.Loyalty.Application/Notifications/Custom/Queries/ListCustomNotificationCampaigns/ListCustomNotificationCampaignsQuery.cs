using KBeauty.Loyalty.Application.Notifications.Custom;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Custom.Queries.ListCustomNotificationCampaigns;

public sealed record ListCustomNotificationCampaignsQuery(
    CustomNotificationCampaignStatus? Status,
    int Take = 100) : IRequest<Result<IReadOnlyList<CustomNotificationCampaignDto>>>;
