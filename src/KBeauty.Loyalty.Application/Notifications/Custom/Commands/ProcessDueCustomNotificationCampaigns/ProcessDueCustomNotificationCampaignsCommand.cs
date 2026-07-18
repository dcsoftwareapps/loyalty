using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Custom.Commands.ProcessDueCustomNotificationCampaigns;

public sealed record ProcessDueCustomNotificationCampaignsCommand(int BatchSize) : IRequest<Result<int>>;
