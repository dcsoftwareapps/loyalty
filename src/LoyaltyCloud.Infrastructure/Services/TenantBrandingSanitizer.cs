using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal static partial class TenantBrandingSanitizer
{
    public const string DefaultPrimaryColor = "#111827";
    public const string DefaultSecondaryColor = "#F3F4F6";
    public const string DefaultDisplayName = "LoyaltyCloud";

    public static string ColorOrDefault(
        string? value,
        string fallback,
        Guid tenantId,
        string fieldName,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var color = value.Trim();
        if (HexColorRegex().IsMatch(color))
            return ExpandShortHex(color);

        logger.LogWarning(
            "Tenant branding color is invalid. TenantId={TenantId}, Field={Field}, Value={Value}; using fallback {Fallback}.",
            tenantId,
            fieldName,
            color,
            fallback);
        return fallback;
    }

    public static string? UrlOrNull(
        string? value,
        Guid tenantId,
        string fieldName,
        ILogger logger,
        params string[] allowedSchemes)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || !allowedSchemes.Any(scheme => string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning(
                "Tenant branding URL is invalid. TenantId={TenantId}, Field={Field}; value was ignored.",
                tenantId,
                fieldName);
            return null;
        }

        return uri.ToString();
    }

    public static string? TextOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    public static string ToRgbColor(string? hex, string fallbackRgb)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallbackRgb;

        var color = ExpandShortHex(hex.Trim());
        if (!HexColorRegex().IsMatch(color) || color.Length != 7)
            return fallbackRgb;

        return int.TryParse(color.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
            && int.TryParse(color.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            && int.TryParse(color.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)
            ? $"rgb({r},{g},{b})"
            : fallbackRgb;
    }

    private static string ExpandShortHex(string color)
    {
        if (color.Length != 4)
            return color;

        return $"#{color[1]}{color[1]}{color[2]}{color[2]}{color[3]}{color[3]}";
    }

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();
}
