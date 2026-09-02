extern alias AdminApp;
using AdminApp::LoyaltyCloud.Admin.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class GiftCardWalletPlatformResolverTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)", GiftCardWalletPlatform.Apple)]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 18_0 like Mac OS X)", GiftCardWalletPlatform.Apple)]
    [InlineData("Mozilla/5.0 (Linux; Android 15; Pixel 9)", GiftCardWalletPlatform.Google)]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", GiftCardWalletPlatform.Unknown)]
    [InlineData(null, GiftCardWalletPlatform.Unknown)]
    public void ResolvesRecipientPlatform(string? userAgent, GiftCardWalletPlatform expected) =>
        Assert.Equal(expected, GiftCardWalletPlatformResolver.Resolve(userAgent));
}
