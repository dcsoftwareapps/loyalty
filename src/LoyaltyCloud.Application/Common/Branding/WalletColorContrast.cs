using System.Globalization;

namespace LoyaltyCloud.Application.Common.Branding;

public static class WalletColorContrast
{
    public const string DefaultBackgroundHex = "#FFFFFF";
    public const string DarkTextHex = "#111827";
    public const string LightTextHex = "#FFFFFF";

    public static string NormalizeHexOrDefault(string? value, string fallback = DefaultBackgroundHex)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var color = value.Trim();
        if (color.Length == 4 && color[0] == '#')
            color = $"#{color[1]}{color[1]}{color[2]}{color[2]}{color[3]}{color[3]}";

        return IsHexColor(color) ? color.ToUpperInvariant() : fallback;
    }

    public static bool IsHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var color = value.Trim();
        if (color.Length != 7 || color[0] != '#')
            return false;

        for (var i = 1; i < color.Length; i++)
        {
            var c = color[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }

        return true;
    }

    public static WalletTextColors ResolveTextColors(string backgroundHex)
    {
        var normalized = NormalizeHexOrDefault(backgroundHex);
        return RelativeLuminance(normalized) <= 0.179
            ? new WalletTextColors(LightTextHex, LightTextHex)
            : new WalletTextColors(DarkTextHex, DarkTextHex);
    }

    public static string ToAppleRgb(string hex)
    {
        var normalized = NormalizeHexOrDefault(hex);
        var r = int.Parse(normalized.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(normalized.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(normalized.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return $"rgb({r},{g},{b})";
    }

    private static double RelativeLuminance(string hex)
    {
        static double Channel(int value)
        {
            var s = value / 255d;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        var r = int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }
}

public sealed record WalletTextColors(string ForegroundHex, string LabelHex);
