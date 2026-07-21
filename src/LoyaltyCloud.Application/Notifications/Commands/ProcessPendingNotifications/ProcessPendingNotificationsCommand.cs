using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Commands.ProcessPendingNotifications;

public sealed record ProcessPendingNotificationsCommand(int BatchSize, int MaxAttempts) : IRequest<Result<int>>;
