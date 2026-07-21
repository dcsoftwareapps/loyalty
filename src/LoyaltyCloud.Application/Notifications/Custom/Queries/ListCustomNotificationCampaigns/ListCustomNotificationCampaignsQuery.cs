using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Enums;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Queries.ListCustomNotificationCampaigns;

public sealed record ListCustomNotificationCampaignsQuery(
    CustomNotificationCampaignStatus? Status,
    int Take = 100) : IRequest<Result<IReadOnlyList<CustomNotificationCampaignDto>>>;
