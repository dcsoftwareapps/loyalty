namespace LoyaltyCloud.Admin.Services;

public enum WalletPlatform
{
    Unknown = 0,
    Apple = 1,
    Google = 2
}

public sealed record BrowserWalletSignal(
    string? UserAgent,
    string? Platform,
    string? Vendor,
    int MaxTouchPoints);

public static class WalletPlatformDetector
{
    public static WalletPlatform Detect(BrowserWalletSignal? signal)
    {
        if (signal is null)
            return WalletPlatform.Unknown;

        var userAgent = signal.UserAgent ?? string.Empty;
        var platform = signal.Platform ?? string.Empty;
        var vendor = signal.Vendor ?? string.Empty;
        var combined = $"{userAgent} {platform} {vendor}";

        if (ContainsAny(combined, "iPhone", "iPad", "iPod"))
            return WalletPlatform.Apple;

        if (platform.Contains("Mac", StringComparison.OrdinalIgnoreCase)
            && signal.MaxTouchPoints > 1
            && userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase))
        {
            return WalletPlatform.Apple;
        }

        if (combined.Contains("Android", StringComparison.OrdinalIgnoreCase))
            return WalletPlatform.Google;

        return WalletPlatform.Unknown;
    }

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
