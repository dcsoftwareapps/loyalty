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
