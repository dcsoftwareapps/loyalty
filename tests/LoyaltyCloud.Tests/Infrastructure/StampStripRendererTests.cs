using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

[Trait("Category", "StampVisualAssets")]
public sealed class StampStripRendererTests
{
    [Fact]
    public void Custom_upcoming_art_is_unmodified_and_is_ignored_for_available_and_redeemed()
    {
        var availableImage = Integration.StampVisualAssetsTests.Png(50, 50, new Rgba32(220, 40, 80));
        var upcomingImage = Integration.StampVisualAssetsTests.Png(50, 50, new Rgba32(20, 160, 230));
        var reward = new StampWalletReward(10, Guid.NewGuid(), availableImage, StampWalletRewardState.Upcoming, upcomingImage);
        var state = new StampWalletProjection(Guid.NewGuid(), Guid.NewGuid(), 4, 10, null, null, [reward]);
        var custom = StampStripRenderer.Render(state, "#FFFFFF");
        var unmodified = StampStripRenderer.Render(state with { Rewards = [reward with { Image = upcomingImage, State = StampWalletRewardState.Available }] }, "#FFFFFF");
        for (var i = 0; i < 3; i++) Assert.Equal(unmodified[i].Bytes, custom[i].Bytes);
        var fallback = StampStripRenderer.Render(state with { Rewards = [reward with { UpcomingImage = null }] }, "#FFFFFF");
        using var gray = Image.Load<Rgba32>(fallback[0].Bytes);
        Assert.Equal(gray[318, 104].R, gray[318, 104].G);
        Assert.Equal(gray[318, 104].G, gray[318, 104].B);
        Assert.NotEqual(custom[0].Bytes, fallback[0].Bytes);
        foreach (var status in new[] { StampWalletRewardState.Available, StampWalletRewardState.Redeemed })
        {
            var withCustom = StampStripRenderer.Render(state with { Rewards = [reward with { State = status }] }, "#FFFFFF");
            var withoutCustom = StampStripRenderer.Render(state with { Rewards = [reward with { State = status, UpcomingImage = null }] }, "#FFFFFF");
            for (var i = 0; i < 3; i++) Assert.Equal(withoutCustom[i].Bytes, withCustom[i].Bytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ten_stamp_grid_uses_normal_art_except_at_explicit_reward_milestones(bool intermediateReward)
    {
        var filled = new Rgba32(20, 70, 220);
        var empty = new Rgba32(30, 180, 60);
        var reward = new Rgba32(220, 40, 80);
        var rewards = new List<StampWalletReward> { new(10, Guid.NewGuid(), Integration.StampVisualAssetsTests.Png(50, 50, reward), StampWalletRewardState.Upcoming) };
        if (intermediateReward) rewards.Add(new(5, Guid.NewGuid(), null, StampWalletRewardState.Upcoming));
        var state = new StampWalletProjection(Guid.NewGuid(), Guid.NewGuid(), 4, 10,
            Integration.StampVisualAssetsTests.Png(50, 50, empty), Integration.StampVisualAssetsTests.Png(50, 50, filled), rewards);

        foreach (var asset in StampStripRenderer.Render(state, "#FFFFFF").Select((asset, index) => (asset, scale: index + 1)))
        {
            using var png = Image.Load<Rgba32>(asset.asset.Bytes);
            // Centers of the fixed 5-column/two-row grid for MaxStamps=10.
            for (var position = 1; position <= 10; position++)
            {
                var x = (int)Math.Round((57.5 + ((position - 1) % 5) * 65) * asset.scale);
                var y = (int)Math.Round((39.5 + ((position - 1) / 5) * 65) * asset.scale);
                var pixel = png[x, y];
                if (position == 10 || intermediateReward && position == 5)
                {
                    Assert.Equal(pixel.R, pixel.G); Assert.Equal(pixel.G, pixel.B);
                    Assert.NotEqual(new Rgba32(255, 255, 255), pixel);
                }
                else Assert.Equal(position <= 4 ? filled : empty, pixel);
            }
        }
    }

    [Theory]
    [InlineData(5, 0)] [InlineData(5, 3)] [InlineData(5, 5)]
    [InlineData(10, 0)] [InlineData(10, 2)] [InlineData(10, 4)] [InlineData(10, 5)] [InlineData(10, 9)] [InlineData(10, 10)]
    [InlineData(12, 0)] [InlineData(12, 6)] [InlineData(12, 12)]
    public void Defaults_and_custom_assets_render_exact_sizes_with_visible_native_pixels(int total, int progress)
    {
        foreach (var custom in new[] { false, true })
        {
            var image = custom ? Integration.StampVisualAssetsTests.Png(400, 20) : null;
            var projection = new StampWalletProjection(Guid.NewGuid(), Guid.NewGuid(), progress, total, image, image,
                [new((total + 1) / 2, Guid.NewGuid(), image), new(total, Guid.NewGuid(), image)]);
            var assets = StampStripRenderer.Render(projection, "rgb(255,255,255)");
            var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                $"../../../../../artifacts/stamps-wallet-production-tests/{total}-{progress}-{(custom ? "custom" : "defaults")}"));
            Directory.CreateDirectory(directory);
            foreach (var asset in assets) File.WriteAllBytes(Path.Combine(directory, asset.Name), asset.Bytes);
            Assert.Equal(3, assets.Count);
            for (var i = 0; i < assets.Count; i++)
            {
                using var png = Image.Load<Rgba32>(assets[i].Bytes);
                Assert.Equal(375 * (i + 1), png.Width); Assert.Equal(144 * (i + 1), png.Height);
                Assert.Equal(new Rgba32(255, 255, 255), png[0, 0]);
                Assert.True(HasArtwork(png));
            }
        }
    }

    [Fact]
    public void Upcoming_is_grayscale_without_overlay_available_is_color_and_redeemed_has_small_neutral_check()
    {
        var identity = Guid.NewGuid();
        var image = Integration.StampVisualAssetsTests.Png(50, 50, new Rgba32(220, 40, 80));
        var state = new StampWalletProjection(Guid.NewGuid(), Guid.NewGuid(), 1, 2, null, null,
            [new(1, identity, image, StampWalletRewardState.Upcoming)]);
        var upcoming = StampStripRenderer.Render(state, "#FFFFFF");
        var available = StampStripRenderer.Render(state with { Rewards = [new(1, identity, image, StampWalletRewardState.Available)] }, "#FFFFFF");
        var redeemed = StampStripRenderer.Render(state with { Rewards = [new(1, identity, image, StampWalletRewardState.Redeemed)] }, "#FFFFFF");
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/stamps-wallet-production-tests/reward-states"));
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "upcoming.png"), upcoming[2].Bytes);
        File.WriteAllBytes(Path.Combine(directory, "available.png"), available[2].Bytes);
        File.WriteAllBytes(Path.Combine(directory, "redeemed.png"), redeemed[2].Bytes);
        for (var n = 0; n < 3; n++)
        {
            var scale = n + 1;
            using var next = Image.Load<Rgba32>(upcoming[n].Bytes); using var ready = Image.Load<Rgba32>(available[n].Bytes); using var used = Image.Load<Rgba32>(redeemed[n].Bytes);
            var gray = next[145 * scale, 60 * scale]; Assert.Equal(gray.R, gray.G); Assert.Equal(gray.G, gray.B);
            Assert.Equal(new Rgba32(220, 40, 80), ready[145 * scale, 60 * scale]);
            Assert.Equal(gray, used[145 * scale, 60 * scale]);
            var differences = 0;
            for (var y = 0; y < next.Height; y++) for (var x = 0; x < next.Width; x++) if (next[x, y] != used[x, y]) differences++;
            Assert.InRange(differences, 30 * scale * scale, 350 * scale * scale);
            // No lock or other overlay anywhere within the upcoming reward's uniform art.
            for (var y = 50 * scale; y < 94 * scale; y++) for (var x = 130 * scale; x < 170 * scale; x++) Assert.Equal(gray, next[x, y]);
        }
    }

    [Fact]
    public void Locked_reward_differs_from_unlocked_even_for_monochrome_image()
    {
        var state = new StampWalletProjection(Guid.NewGuid(), Guid.NewGuid(), 4, 10, null, null,
            [new(5, Guid.NewGuid(), Integration.StampVisualAssetsTests.Png(50, 50, new Rgba32(40, 40, 40))), new(10, Guid.NewGuid(), null)]);
        var locked = StampStripRenderer.Render(state, "#FFFFFF")[0].Bytes;
        var unlocked = StampStripRenderer.Render(state with { CurrentStamps = 5 }, "#FFFFFF")[0].Bytes;
        Assert.False(locked.SequenceEqual(unlocked));
    }

    [Fact]
    public async Task Blob_store_rejects_other_tenant_path_before_any_storage_access()
    {
        var store = new StampVisualAssetStore(Options.Create(new AzureStorageOptions()));
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync(b,
            $"{TenantBrandingLogoService.GetTenantBrandingPrefix(a)}/stamps/image.png", CancellationToken.None));
    }
    private static bool HasArtwork(Image<Rgba32> image)
    {
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++) if (image[x, y] != image[0, 0]) return true;
        return false;
    }
}
