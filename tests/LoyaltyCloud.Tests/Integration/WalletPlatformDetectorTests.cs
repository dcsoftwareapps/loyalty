extern alias AdminApp;

using AdminApp::LoyaltyCloud.Admin.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class WalletPlatformDetectorTests
{
    [Fact]
    public void Detects_iPhone_as_Apple()
    {
        var platform = WalletPlatformDetector.Detect(new BrowserWalletSignal(
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148 Safari/604.1",
            "iPhone",
            "Apple Computer, Inc.",
            5));

        Assert.Equal(WalletPlatform.Apple, platform);
    }

    [Fact]
    public void Detects_iPad_as_Apple()
    {
        var platform = WalletPlatformDetector.Detect(new BrowserWalletSignal(
            "Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148 Safari/604.1",
            "iPad",
            "Apple Computer, Inc.",
            5));

        Assert.Equal(WalletPlatform.Apple, platform);
    }

    [Fact]
    public void Detects_iPadOS_desktop_user_agent_as_Apple_when_touch_is_available()
    {
        var platform = WalletPlatformDetector.Detect(new BrowserWalletSignal(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15) AppleWebKit/605.1.15 Version/17.0 Safari/605.1.15",
            "MacIntel",
            "Apple Computer, Inc.",
            5));

        Assert.Equal(WalletPlatform.Apple, platform);
    }

    [Fact]
    public void Detects_Android_as_Google()
    {
        var platform = WalletPlatformDetector.Detect(new BrowserWalletSignal(
            "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/125.0 Mobile Safari/537.36",
            "Linux armv8l",
            "Google Inc.",
            5));

        Assert.Equal(WalletPlatform.Google, platform);
    }

    [Fact]
    public void Detects_desktop_as_Unknown()
    {
        var platform = WalletPlatformDetector.Detect(new BrowserWalletSignal(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/125.0 Safari/537.36",
            "Win32",
            "Google Inc.",
            0));

        Assert.Equal(WalletPlatform.Unknown, platform);
    }
}
