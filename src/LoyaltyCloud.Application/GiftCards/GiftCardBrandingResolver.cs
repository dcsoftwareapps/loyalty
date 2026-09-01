using LoyaltyCloud.Application.Common.Branding;

namespace LoyaltyCloud.Application.GiftCards;

public sealed record EffectiveGiftCardBranding(string BackgroundColor, string TextColor, string DisplayName, string? LogoUrl);

public static class GiftCardBrandingResolver
{
    public const string DefaultBackgroundColor = "#1C1B18";
    public const string DefaultDisplayName = "Gift Card";

    public static EffectiveGiftCardBranding Resolve(
        string? giftCardBackground,
        string? giftCardText,
        string? giftCardDisplayName,
        string? giftCardLogoUrl,
        string? tenantBackground = null,
        string? tenantDisplayName = null,
        string? tenantLogoUrl = null)
    {
        var background = WalletColorContrast.IsHexColor(giftCardBackground)
            ? WalletColorContrast.NormalizeHexOrDefault(giftCardBackground)
            : WalletColorContrast.NormalizeHexOrDefault(tenantBackground, DefaultBackgroundColor);
        var text = WalletColorContrast.IsHexColor(giftCardText)
            ? WalletColorContrast.NormalizeHexOrDefault(giftCardText)
            : WalletColorContrast.ResolveTextColors(background).ForegroundHex;
        var name = !string.IsNullOrWhiteSpace(giftCardDisplayName)
            ? giftCardDisplayName.Trim()
            : !string.IsNullOrWhiteSpace(tenantDisplayName) ? tenantDisplayName.Trim() : DefaultDisplayName;
        var logo = !string.IsNullOrWhiteSpace(giftCardLogoUrl)
            ? giftCardLogoUrl.Trim()
            : string.IsNullOrWhiteSpace(tenantLogoUrl) ? null : tenantLogoUrl.Trim();
        return new(background, text, name, logo);
    }
}