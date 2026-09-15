namespace LoyaltyCloud.Cashier.Services;

public sealed class MauiMonetaryRedemptionPendingStore(ISecureTokenStore secureStore) : IMonetaryRedemptionPendingStore
{
    public Task<string?> GetAsync(string key) => secureStore.GetAsync(key);

    public Task SetAsync(string key, string value) => secureStore.SetAsync(key, value);
}
