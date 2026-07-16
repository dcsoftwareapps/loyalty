using KBeauty.Loyalty.Application.Notifications.PointCampaign;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CreatePointCampaignStartedNotifications;

public sealed record CreatePointCampaignStartedNotificationsCommand(
    string OperatorId,
    string TimeZoneId = "America/Tijuana") : IRequest<Result<CreatePointCampaignStartedNotificationsResponse>>;
