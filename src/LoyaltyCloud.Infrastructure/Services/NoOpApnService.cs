using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class NoOpApnService : IApnService
{
    private readonly ILogger<NoOpApnService> _logger;

    public NoOpApnService(ILogger<NoOpApnService> logger)
    {
        _logger = logger;
    }

    public Task SendPassUpdateAsync(string pushToken, PassUpdateReason reason, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "APNs skipped because NoOpApnService is registered. reason={Reason}, token={Token}.",
            reason,
            SafePushToken(pushToken));
        return Task.CompletedTask;
    }

    private static string SafePushToken(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "empty"
            : value.Length <= 12
                ? $"{value[..Math.Min(value.Length, 4)]}..."
                : $"{value[..6]}...{value[^6..]}";
}
