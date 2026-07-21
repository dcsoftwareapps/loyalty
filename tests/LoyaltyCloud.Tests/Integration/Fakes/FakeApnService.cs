using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Tests.Integration.Fakes;

/// <summary>APN que solo cuenta llamadas — los tests pueden inspeccionarlas.</summary>
public sealed class FakeApnService : IApnService
{
    public List<(string Token, PassUpdateReason Reason)> Calls { get; } = new();

    public Task SendPassUpdateAsync(string pushToken, PassUpdateReason reason, CancellationToken ct = default)
    {
        Calls.Add((pushToken, reason));
        return Task.CompletedTask;
    }
}
