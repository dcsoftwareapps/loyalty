using LoyaltyCloud.Application.Notifications.PointCampaign;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Commands.CreatePointCampaignStartedNotifications;

public sealed record CreatePointCampaignStartedNotificationsCommand(
    string OperatorId,
    string TimeZoneId = "America/Tijuana") : IRequest<Result<CreatePointCampaignStartedNotificationsResponse>>;
