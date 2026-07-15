using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Commands.ProcessPendingNotifications;

public sealed record ProcessPendingNotificationsCommand(int BatchSize, int MaxAttempts) : IRequest<Result<int>>;
