namespace LoyaltyCloud.Cashier.Services;

// Separate keys from bearer/session storage; never store a password or token in the pending payload.
public sealed class MauiRewardRedemptionPendingStore(ISecureTokenStore secureStore) : IRewardRedemptionPendingStore
{
    public Task<string?> GetAsync(string key) => secureStore.GetAsync(key);
    public Task SetAsync(string key, string value) => secureStore.SetAsync(key, value);
}
