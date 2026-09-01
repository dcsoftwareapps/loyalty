using LoyaltyCloud.Application.GiftCards;
using Xunit;

namespace LoyaltyCloud.Tests.Application;

public sealed class GiftCardBrandingResolverTests
{
    [Fact]
    public void ExplicitGiftCardColorsAndIdentityWin()
    {
        var result = GiftCardBrandingResolver.Resolve("#123456", "#ABCDEF", "Regalo", "https://gift/logo.png", "#FFFFFF", "Tenant", "https://tenant/logo.png");
        Assert.Equal("#123456", result.BackgroundColor);
        Assert.Equal("#ABCDEF", result.TextColor);
        Assert.Equal("Regalo", result.DisplayName);
        Assert.Equal("https://gift/logo.png", result.LogoUrl);
    }

    [Fact]
    public void MissingGiftCardBrandingInheritsTenantPresentation()
    {
        var result = GiftCardBrandingResolver.Resolve(null, null, null, null, "#334455", "Tamalitos", "https://tenant/logo.png");
        Assert.Equal("#334455", result.BackgroundColor);
        Assert.Equal("#FFFFFF", result.TextColor);
        Assert.Equal("Tamalitos", result.DisplayName);
        Assert.Equal("https://tenant/logo.png", result.LogoUrl);
    }

    [Theory]
    [InlineData("#050505", "#FFFFFF")]
    [InlineData("#FAFAFA", "#111827")]
    [InlineData("not-a-color", "#FFFFFF")]
    public void MissingOrMalformedTextUsesReadableContrast(string background, string expectedText)
    {
        var result = GiftCardBrandingResolver.Resolve(background, null, null, null);
        Assert.Equal(expectedText, result.TextColor);
    }

    [Fact]
    public void ResolutionDoesNotRetainAnotherTenantState()
    {
        var tenantA = GiftCardBrandingResolver.Resolve(null, null, null, null, "#112233", "Tenant A", null);
        var tenantB = GiftCardBrandingResolver.Resolve(null, null, null, null, "#F5F5F5", "Tenant B", null);
        Assert.NotEqual(tenantA.BackgroundColor, tenantB.BackgroundColor);
        Assert.Equal("Tenant B", tenantB.DisplayName);
    }
}
