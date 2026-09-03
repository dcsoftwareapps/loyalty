namespace LoyaltyCloud.Admin.Services;

public enum GiftCardWalletPlatform { Unknown, Apple, Google }

public static class GiftCardWalletPlatformResolver
{
    public static GiftCardWalletPlatform Resolve(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return GiftCardWalletPlatform.Unknown;
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPod", StringComparison.OrdinalIgnoreCase))
            return GiftCardWalletPlatform.Apple;
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
            return GiftCardWalletPlatform.Google;
        return GiftCardWalletPlatform.Unknown;
    }
}
