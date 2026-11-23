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
        Assert.True(HasMutedArtwork(gray));
        Assert.NotEqual(custom[0].Bytes, fallback[0].Bytes);
        foreach (var status in new[] { StampWalletRewardState.Available, StampWalletRewardState.Redeemed })
        {
            var withCustom = StampStripRenderer.Render(state with { Rewards = [reward with { State = status }] }, "#FFFFFF");
            var withoutCustom = StampStripRenderer.Render(state with { Rewards = [reward with { State = status, UpcomingImage = null }] }, "#FFFFFF");
            for (var i = 0; i < 3; i++) Assert.Equal(withoutCustom[i].Bytes, withCustom[i].Bytes);
        }
    }

    [Theory]
    [InlineData(9, 0, false)] [InlineData(9, 3, false)] [InlineData(9, 9, false)]
    [InlineData(5, 0, false)] [InlineData(5, 3, false)] [InlineData(5, 5, false)]
    [InlineData(9, 4, true)] [InlineData(5, 2, true)] [InlineData(12, 6, true)]
    public void Every_stamp_has_its_own_artwork_and_rewards_are_additional_milestone_cells(int total, int progress, bool intermediate)
    {
        AssertIndependentCells(total, progress, intermediate ? [total / 2, total] : [total]);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)]
    public void All_supported_totals_fit_even_with_a_reward_at_every_milestone(int total)
    {
        AssertIndependentCells(total, total / 2, Enumerable.Range(1, total).ToArray());
    }

    private static void AssertIndependentCells(int total, int progress, int[] thresholds)
    {
        var filled = new Rgba32(20, 70, 220);
        var empty = new Rgba32(30, 180, 60);
        var reward = new Rgba32(220, 40, 80);
        var art = Integration.StampVisualAssetsTests.Png(50, 50, reward);
        var rewards = thresholds.Select(n => new StampWalletReward(n, Guid.NewGuid(), art,
            progress >= n ? StampWalletRewardState.Available : StampWalletRewardState.Upcoming, art)).ToList();
        var state = new StampWalletProjection(Guid.NewGuid(), Guid.NewGuid(), progress, total,
            Integration.StampVisualAssetsTests.Png(50, 50, empty), Integration.StampVisualAssetsTests.Png(50, 50, filled), rewards);
        var before = System.Text.Json.JsonSerializer.Serialize(state);
        foreach (var asset in StampStripRenderer.Render(state, "#FFFFFF"))
        {
            using var png = Image.Load<Rgba32>(asset.Bytes);
            // Count connected solid-color images, independently of the renderer's grid arithmetic.
            var filledCells = ColorRegions(png, filled);
            var emptyCells = ColorRegions(png, empty);
            var rewardCells = ColorRegions(png, reward);
            Assert.Equal(progress, filledCells.Count);
            Assert.Equal(total - progress, emptyCells.Count);
            Assert.Equal(thresholds.Length, rewardCells.Count);
            Assert.Equal(total + thresholds.Length, filledCells.Count + emptyCells.Count + rewardCells.Count);
            var ordered = filledCells.Concat(emptyCells).Select(p => (Position: p, IsReward: false))
                .Concat(rewardCells.Select(p => (Position: p, IsReward: true)))
                .OrderBy(x => x.Position.Y).ThenBy(x => x.Position.X).ToList();
            var stampsSeen = 0; var rewardIndex = 0;
            foreach (var cell in ordered)
                if (cell.IsReward) Assert.Equal(thresholds[rewardIndex++], stampsSeen);
                else stampsSeen++;
        }
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(state));
    }

    private static List<Point> ColorRegions(Image<Rgba32> image, Rgba32 color)
    {
        var regions = new List<Point>();
        var visited = new bool[image.Width * image.Height];
        var queue = new Queue<Point>();
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            if (visited[y * image.Width + x] || image[x, y] != color) continue;
            regions.Add(new Point(x, y));
            Visit(x, y);
            while (queue.TryDequeue(out var point))
            {
                Visit(point.X - 1, point.Y); Visit(point.X + 1, point.Y);
                Visit(point.X, point.Y - 1); Visit(point.X, point.Y + 1);
            }
        }
        return regions;
        void Visit(int x, int y)
        {
            if (x < 0 || y < 0 || x >= image.Width || y >= image.Height) return;
            var index = y * image.Width + x;
            if (visited[index] || image[x, y] != color) return;
            visited[index] = true;
            queue.Enqueue(new Point(x, y));
        }
    }

    private static bool HasMutedArtwork(Image<Rgba32> image)
    {
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var p = image[x, y];
            if (p.R == p.G && p.G == p.B && p.R < 200 && p.A == 255) return true;
        }
        return false;
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
            var gray = next[187 * scale, 60 * scale]; Assert.Equal(gray.R, gray.G); Assert.Equal(gray.G, gray.B);
            Assert.Equal(new Rgba32(220, 40, 80), ready[187 * scale, 60 * scale]);
            Assert.Equal(gray, used[187 * scale, 60 * scale]);
            var differences = 0;
            for (var y = 0; y < next.Height; y++) for (var x = 0; x < next.Width; x++) if (next[x, y] != used[x, y]) differences++;
            Assert.InRange(differences, 30 * scale * scale, 350 * scale * scale);
            // No lock or other overlay anywhere within the upcoming reward's uniform art.
            for (var y = 50 * scale; y < 70 * scale; y++) for (var x = 175 * scale; x < 190 * scale; x++) Assert.Equal(gray, next[x, y]);
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
