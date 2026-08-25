using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Tests.Integration.Fakes;

/// <summary>APN que solo cuenta llamadas — los tests pueden inspeccionarlas.</summary>
public sealed class FakeApnService : IApnService
{
    public List<(string Token, PassUpdateReason Reason)> Calls { get; } = new();
    public bool FailSends { get; set; }
    public ApnPushResult? NextResult { get; set; }

    public Task<ApnPushResult> SendPassUpdateAsync(string pushToken, PassUpdateReason reason, CancellationToken ct = default)
    {
        Calls.Add((pushToken, reason));
        if (FailSends)
            return Task.FromResult(ApnPushResult.Transient(500, "Fake APNs failure."));

        return Task.FromResult(NextResult ?? ApnPushResult.Accepted(200));
    }
}
