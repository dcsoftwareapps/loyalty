using System.Text.Json;

namespace LoyaltyCloud.Cashier.Services;

public interface ISecureTokenStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    void Remove(string key);
}

public sealed class CashierSessionService
{
    private const string SessionKey = "loyaltycloud.cashier.session.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISecureTokenStore _store;

    public CashierSession? Current { get; private set; }

    public event Action? SessionChanged;

    public CashierSessionService(ISecureTokenStore store)
    {
        _store = store;
    }

    public async Task<CashierSession?> RestoreAsync()
    {
        var raw = await _store.GetAsync(SessionKey);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        CashierSession? session;
        try
        {
            using var document = JsonDocument.Parse(raw);
            if (!document.RootElement.TryGetProperty("programType", out _))
            {
                await LogoutAsync();
                return null;
            }
            session = JsonSerializer.Deserialize<CashierSession>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            await LogoutAsync();
            return null;
        }

        if (session is null || session.ProgramType is not ("Points" or "Stamps") || session.IsExpired(DateTimeOffset.UtcNow))
        {
            await LogoutAsync();
            return null;
        }

        Current = session;
        SessionChanged?.Invoke();
        return Current;
    }

    public async Task SignInAsync(CashierSession session)
    {
        Current = session;
        await _store.SetAsync(SessionKey, JsonSerializer.Serialize(session, JsonOptions));
        SessionChanged?.Invoke();
    }

    public Task LogoutAsync()
    {
        Current = null;
        _store.Remove(SessionKey);
        SessionChanged?.Invoke();
        return Task.CompletedTask;
    }

    public async Task ClearAfterUnauthorizedAsync()
    {
        await LogoutAsync();
    }
}
