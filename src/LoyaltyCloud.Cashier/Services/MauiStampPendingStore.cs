using LoyaltyCloud.Common.Services;

namespace LoyaltyCloud.Cashier.Services;

public sealed class MauiStampPendingStore(ISecureTokenStore store) : IStampPendingStore
{
    public Task<string?> GetAsync(string key) => store.GetAsync(key);
    public Task SetAsync(string key, string value) => store.SetAsync(key, value);
    public Task RemoveAsync(string key) { store.Remove(key); return Task.CompletedTask; }
}
