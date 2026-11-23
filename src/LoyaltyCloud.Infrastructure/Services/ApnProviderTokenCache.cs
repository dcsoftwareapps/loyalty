namespace LoyaltyCloud.Infrastructure.Services;

/// <summary>Process-wide APNs provider JWT cache. Never retains a scoped token factory or signing key.</summary>
internal sealed class ApnProviderTokenCache : IDisposable
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(50);
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private CachedToken? _token;

    public ApnProviderTokenCache() : this(TimeProvider.System) { }

    internal ApnProviderTokenCache(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public async Task<string> GetOrCreateAsync(Func<CancellationToken, Task<string>> createToken, CancellationToken ct = default)
    {
        var token = Volatile.Read(ref _token);
        if (token is not null && _timeProvider.GetUtcNow() < token.RenewAtUtc)
            return token.Jwt;

        await _tokenLock.WaitAsync(ct);
        try
        {
            token = Volatile.Read(ref _token);
            if (token is not null && _timeProvider.GetUtcNow() < token.RenewAtUtc)
                return token.Jwt;

            var jwt = await createToken(ct);
            // Publish the JWT and renewal deadline together, only after successful generation.
            Volatile.Write(ref _token, new CachedToken(jwt, _timeProvider.GetUtcNow().Add(TokenLifetime)));
            return jwt;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public void Dispose() => _tokenLock.Dispose();

    private sealed record CachedToken(string Jwt, DateTimeOffset RenewAtUtc);
}
