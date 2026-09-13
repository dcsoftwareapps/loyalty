namespace LoyaltyCloud.Cashier.Services;

public sealed class CashierLoginPreferences
{
    private const string TenantKey = "loyaltycloud.cashier.login.tenant";
    private const string UsernameKey = "loyaltycloud.cashier.login.username";

    public (bool RememberMe, string TenantSlug, string Username) Load()
    {
        var tenant = Preferences.Default.Get(TenantKey, string.Empty);
        var username = Preferences.Default.Get(UsernameKey, string.Empty);
        return (!string.IsNullOrEmpty(tenant) && !string.IsNullOrEmpty(username), tenant, username);
    }

    // Only login identifiers belong in Preferences; tokens remain in SecureStorage.
    public void Save(bool rememberMe, string tenantSlug, string username)
    {
        if (rememberMe)
        {
            Preferences.Default.Set(TenantKey, tenantSlug.Trim());
            Preferences.Default.Set(UsernameKey, username.Trim());
        }
        else
        {
            Preferences.Default.Remove(TenantKey);
            Preferences.Default.Remove(UsernameKey);
        }
    }
}
