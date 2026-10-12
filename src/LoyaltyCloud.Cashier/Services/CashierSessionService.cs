using System.Text.Json;

namespace LoyaltyCloud.Cashier.Services;

public interface ISecureTokenStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key,string value);
    void Remove(string key);
}

public sealed class CashierSessionService
{
    private const string SessionKey="loyaltycloud.cashier.session.v1";
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);
    private readonly ISecureTokenStore _store;
    private readonly CashierApiClient? _api;
    private readonly SemaphoreSlim _gate=new(1,1);
    public CashierSession? Current { get; private set; }
    public event Action? SessionChanged;
    public CashierSessionService(ISecureTokenStore store,CashierApiClient? api=null) { _store=store; _api=api; }

    public async Task<CashierSession?> RestoreAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (Current is not null && !Current.IsExpired(DateTimeOffset.UtcNow)) return Current;
            var raw=await _store.GetAsync(SessionKey);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            CashierSession? session;
            try
            {
                using var document=JsonDocument.Parse(raw);
                if (!document.RootElement.TryGetProperty("programType",out _)) { ClearLocal(); return null; }
                session=JsonSerializer.Deserialize<CashierSession>(raw,JsonOptions);
            }
            catch (JsonException) { ClearLocal(); return null; }
            if (session is null || string.IsNullOrWhiteSpace(session.AccessToken) || session.ProgramType is not ("Points" or "Stamps")) { ClearLocal(); return null; }
            Current=session;
            if (session.IsExpired(DateTimeOffset.UtcNow))
            {
                if (!await RefreshCoreAsync(session.AccessToken,CancellationToken.None)) return null;
            }
            SessionChanged?.Invoke(); return Current;
        }
        finally { _gate.Release(); }
    }

    public async Task SignInAsync(CashierSession session,bool rememberMe=true)
    {
        await _gate.WaitAsync();
        try
        {
            var value=(rememberMe ? session : session with { RefreshToken=null,RefreshTokenExpiresAtUtc=null }) with { SessionId=Guid.NewGuid().ToString("N") };
            if (rememberMe) await _store.SetAsync(SessionKey,JsonSerializer.Serialize(value,JsonOptions));
            else _store.Remove(SessionKey);
            Current=value; SessionChanged?.Invoke();
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> EnsureAccessAsync(CancellationToken ct=default)
    {
        var current=Current;
        if (current is null) return false;
        return !current.IsExpired(DateTimeOffset.UtcNow) || await RefreshAfterUnauthorizedAsync(current.AccessToken,ct);
    }

    public async Task<bool> RefreshAfterUnauthorizedAsync(string rejectedAccessToken,CancellationToken ct=default)
    {
        await _gate.WaitAsync(ct);
        try { return await RefreshCoreAsync(rejectedAccessToken,ct); }
        finally { _gate.Release(); }
    }
    private async Task<bool> RefreshCoreAsync(string rejectedAccessToken,CancellationToken ct)
    {
        var current=Current;
        if (current is null) return false;
        // Other requests may already have refreshed this exact access token.
        if (current.AccessToken!=rejectedAccessToken && !current.IsExpired(DateTimeOffset.UtcNow)) return true;
        if (_api is null || string.IsNullOrWhiteSpace(current.RefreshToken)
            || current.RefreshTokenExpiresAtUtc<=DateTimeOffset.UtcNow) { ClearLocal(); return false; }
        CashierSession? renewed;
        try { renewed=await _api.RefreshAsync(current.RefreshToken,ct); }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) { throw new HttpRequestException("Session refresh timed out.",ex); }
        // Network/5xx failure propagates without deleting the persistent credentials.
        if (renewed is null || renewed.UserId!=current.UserId || renewed.TenantSlug!=current.TenantSlug) { ClearLocal(); return false; }
        renewed=renewed with { SessionId=current.SessionId };
        await _store.SetAsync(SessionKey,JsonSerializer.Serialize(renewed,JsonOptions));
        Current=renewed; SessionChanged?.Invoke(); return true;
    }

    public async Task LogoutAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var refresh=Current?.RefreshToken;
            ClearLocal(); // Logout remains local and final even when the server is unreachable.
            if (_api is not null && refresh is not null)
            {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await _api.RevokeAsync(refresh,timeout.Token); }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }
            }
        }
        finally { _gate.Release(); }
    }
    public async Task ClearAfterUnauthorizedAsync(string? rejectedAccessToken=null)
    {
        await _gate.WaitAsync();
        try { if (rejectedAccessToken is null || Current?.AccessToken==rejectedAccessToken) ClearLocal(); }
        finally { _gate.Release(); }
    }
    private void ClearLocal() { Current=null; _store.Remove(SessionKey); SessionChanged?.Invoke(); }
}
