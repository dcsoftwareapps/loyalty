using LoyaltyCloud.Infrastructure.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

[Trait("Category", "APNs")]
public sealed class ApnProviderTokenCacheTests
{
    [Theory]
    [InlineData(49, 1)]
    [InlineData(50, 2)]
    [InlineData(51, 2)]
    public async Task Token_is_reused_until_the_fifty_minute_renewal_deadline(int minutes, int expectedGenerations)
    {
        var clock = new Clock();
        using var cache = new ApnProviderTokenCache(clock);
        var generations = 0;
        Task<string> Create(CancellationToken _) => Task.FromResult($"token-{++generations}");
        Assert.Equal("token-1", await cache.GetOrCreateAsync(Create));
        clock.Advance(TimeSpan.FromMinutes(minutes));
        Assert.Equal($"token-{expectedGenerations}", await cache.GetOrCreateAsync(Create));
        Assert.Equal(expectedGenerations, generations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_requests_generate_only_one_token_on_initialization_or_renewal(bool renewal)
    {
        var clock = new Clock();
        using var cache = new ApnProviderTokenCache(clock);
        if (renewal)
        {
            await cache.GetOrCreateAsync(_ => Task.FromResult("old-token"));
            clock.Advance(TimeSpan.FromMinutes(50));
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generations = 0;
        async Task<string> Create(CancellationToken _)
        {
            Interlocked.Increment(ref generations);
            entered.SetResult();
            await release.Task;
            return "shared-token";
        }
        var requests = Enumerable.Range(0, 32).Select(_ => cache.GetOrCreateAsync(Create)).ToArray();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, generations);
        release.SetResult();
        var tokens = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(tokens, token => Assert.Equal("shared-token", token));
        Assert.Equal(1, generations);
    }

    [Fact]
    public async Task Failed_generation_does_not_publish_a_token_or_keep_the_lock()
    {
        var clock = new Clock();
        using var cache = new ApnProviderTokenCache(clock);
        await cache.GetOrCreateAsync(_ => Task.FromResult("old-token"));
        clock.Advance(TimeSpan.FromMinutes(50));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync(
            _ => Task.FromException<string>(new InvalidOperationException("Signing failed."))));
        var token = await cache.GetOrCreateAsync(_ => Task.FromResult("recovered-token"))
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("recovered-token", token);
    }

    [Fact]
    public async Task Cancelled_generation_releases_the_lock_and_allows_the_next_request()
    {
        using var cache = new ApnProviderTokenCache();
        using var cancelled = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetOrCreateAsync(async ct =>
        {
            cancelled.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return "unreachable";
        }, cancelled.Token));
        Assert.Equal("next-token", await cache.GetOrCreateAsync(_ => Task.FromResult("next-token"))
            .WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan elapsed) => _utcNow += elapsed;
    }
}
