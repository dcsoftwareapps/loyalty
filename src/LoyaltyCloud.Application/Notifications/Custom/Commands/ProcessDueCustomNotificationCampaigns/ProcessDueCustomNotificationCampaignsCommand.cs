using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Custom.Commands.ProcessDueCustomNotificationCampaigns;

public sealed record ProcessDueCustomNotificationCampaignsCommand(int BatchSize) : IRequest<Result<int>>;
