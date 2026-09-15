using System.Text.RegularExpressions;

namespace LoyaltyCloud.Cashier.Services;

public static class GiftCardQrParser
{
    private static readonly Regex CodePattern = new(@"\AGC-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TokenPattern = new(@"\A[A-Za-z0-9_-]{1,256}\z", RegexOptions.CultureInvariant);

    public static GiftCardLookup? Parse(string? payload)
    {
        var value = payload?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > 2048) return null;
        if (CodePattern.IsMatch(value)) return new(value.ToUpperInvariant(), null);
        // This only extracts an identifier. No scanned host is ever contacted.
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return null;
        const string prefix = "/giftcards/claim/";
        if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var token = Uri.UnescapeDataString(uri.AbsolutePath[prefix.Length..]);
        return TokenPattern.IsMatch(token) ? new(null, token) : null;
    }
}
