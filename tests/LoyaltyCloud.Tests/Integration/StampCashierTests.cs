using LoyaltyCloud.Common.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampCashier")]
public sealed class StampCashierTests
{
    [Fact]
    public async Task Concurrent_callbacks_are_ignored_while_customer_is_resolving()
    {
        var pause = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new FakeApi { PauseRead = pause.Task };
        var flow = new StampCashierFlow(api, new Store(), "a");
        var first = flow.AcceptScanAsync("KB-TEST");
        await flow.AcceptScanAsync("KB-TEST");
        pause.SetResult();
        await first;
        Assert.Single(api.Calls);
    }
    [Fact]
    public async Task Repeated_scan_callbacks_and_retry_preserve_one_operation_across_restore()
    {
        var api = new FakeApi { Uncertain = true };
        var store = new Store();
        var flow = new StampCashierFlow(api, store, "tenant:user:environment");
        await flow.AcceptScanAsync("KB-TEST");
        var original = Assert.IsType<PendingStampOperation>(flow.Pending);
        await flow.AcceptScanAsync("KB-TEST");
        Assert.Single(api.Calls);
        Assert.True(flow.ScanConsumed);
        flow.PrepareNextScan();
        Assert.True(flow.ScanConsumed);
        var restored = new StampCashierFlow(api, store, "tenant:user:environment");
        await restored.RestoreAsync();
        Assert.Equal(original, restored.Pending);
        api.Uncertain = false;
        await restored.RetryAsync();
        Assert.Equal(original.IdempotencyKey, api.Calls[1].IdempotencyKey);
        Assert.Null(restored.Pending);
        Assert.Equal("Sello agregado", restored.Message);
        await restored.AcceptScanAsync("KB-TEST");
        Assert.Equal(2, api.Calls.Count);
        restored.PrepareNextScan();
        await restored.AcceptScanAsync("KB-OTHER");
        Assert.NotEqual(original.IdempotencyKey, api.Calls[2].IdempotencyKey);
    }

    [Fact]
    public async Task Manual_lookup_is_read_only_and_invalid_qr_never_earns()
    {
        var api = new FakeApi();
        var flow = new StampCashierFlow(api, new Store(), "a");
        await flow.AcceptScanAsync(null);
        Assert.Empty(api.Calls);
        flow.PrepareNextScan();
        await flow.LookupAsync("KB-TEST");
        Assert.Empty(api.Calls);
        await flow.BeginAsync("Earn");
        await flow.BeginAsync("Earn");
        Assert.Single(api.Calls);
        Assert.True(flow.ScanConsumed);
    }

    [Fact]
    public async Task Completed_card_keeps_full_progress_and_unlocked_rewards()
    {
        var api = new FakeApi { Completed = true };
        var flow = new StampCashierFlow(api, new Store(), "a");
        await flow.AcceptScanAsync("KB-TEST");
        Assert.Equal(10, flow.State!.CurrentStamps);
        Assert.Equal(10, flow.State.MaxStamps);
        Assert.Equal("¡Tarjeta completada!", flow.Message);
        Assert.Equal("Producto gratis", Assert.Single(flow.Unlocked).Name);
    }

    [Fact]
    public async Task Intermediate_unlocked_reward_has_priority_over_normal_earn_message()
    {
        var api = new FakeApi { Unlock = true };
        var flow = new StampCashierFlow(api, new Store(), "a");
        await flow.AcceptScanAsync("KB-TEST");
        Assert.Equal("¡Recompensa desbloqueada!", flow.Message);
        Assert.Single(flow.Unlocked);
        Assert.False(flow.State!.CycleCompleted);
    }

    [Theory]
    [InlineData("Redeem")]
    [InlineData("Reversal")]
    public async Task Reward_and_reversal_retries_reuse_key_and_specific_reward(string operation)
    {
        var api = new FakeApi();
        var flow = new StampCashierFlow(api, new Store(), "a");
        await flow.LookupAsync("KB-TEST");
        api.Uncertain = true;
        var rewardId = operation == "Redeem" ? Guid.NewGuid() : (Guid?)null;
        await flow.BeginAsync(operation, rewardId);
        api.Uncertain = false;
        await flow.RetryAsync();
        Assert.Equal(api.Calls[0], api.Calls[1]);
        Assert.Equal(rewardId, api.Calls[0].RewardId);
        Assert.Null(flow.Pending);
    }

    [Fact]
    public async Task Definitive_business_error_clears_pending_without_restarting_scan()
    {
        var api = new FakeApi { BusinessError = "Una recompensa ya fue canjeada." };
        var flow = new StampCashierFlow(api, new Store(), "a");
        await flow.AcceptScanAsync("KB-TEST");
        Assert.Equal(api.BusinessError, flow.Error);
        Assert.Null(flow.Pending);
        Assert.True(flow.ScanConsumed);
    }

    [Fact]
    public async Task Different_operator_or_environment_does_not_restore_another_pending_operation()
    {
        var api = new FakeApi { Uncertain = true };
        var store = new Store();
        await new StampCashierFlow(api, store, "tenant:alice:stg").AcceptScanAsync("KB-TEST");
        var other = new StampCashierFlow(api, store, "tenant:bob:stg");
        await other.RestoreAsync();
        Assert.Null(other.Pending);
    }

    private sealed class Store : IStampPendingStore
    {
        private readonly Dictionary<string, string> values = [];
        public Task<string?> GetAsync(string key) => Task.FromResult(values.GetValueOrDefault(key));
        public Task SetAsync(string key, string value) { values[key] = value; return Task.CompletedTask; }
        public Task RemoveAsync(string key) { values.Remove(key); return Task.CompletedTask; }
    }
    private sealed class FakeApi : IStampCashierApi
    {
        public bool Uncertain { get; set; }
        public bool Completed { get; set; }
        public bool Unlock { get; set; }
        public string? BusinessError { get; set; }
        public Task? PauseRead { get; set; }
        public List<PendingStampOperation> Calls { get; } = [];
        private CashierStampState State(string serial) => new(serial, "Cliente", Guid.NewGuid(), 1,
            Completed ? 10 : 5, 10, Completed, [], true);
        public async Task<StampCall<CashierStampState>> ReadAsync(string serial)
        {
            if (PauseRead is not null) await PauseRead;
            return new(State(serial));
        }
        public Task<StampCall<CashierStampResult>> ExecuteAsync(PendingStampOperation operation)
        {
            Calls.Add(operation);
            return Task.FromResult(Uncertain ? new StampCall<CashierStampResult>(null, Uncertain: true)
                : BusinessError is not null ? new(null, BusinessError)
                : new(new(State(operation.SerialNumber), Completed || Unlock ? [new(Guid.NewGuid(), "Producto gratis", null, 10, DateTime.UtcNow)] : [])));
        }
    }
}
