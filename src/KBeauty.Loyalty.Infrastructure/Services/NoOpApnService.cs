using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Infrastructure.Services;

internal sealed class NoOpApnService : IApnService
{
    private readonly ILogger<NoOpApnService> _logger;

    public NoOpApnService(ILogger<NoOpApnService> logger)
    {
        _logger = logger;
    }

    public Task SendPassUpdateAsync(string pushToken, PassUpdateReason reason, CancellationToken ct = default)
    {
        _logger.LogDebug("APN omitido en modo Development mock ({Reason}).", reason);
        return Task.CompletedTask;
    }
}
