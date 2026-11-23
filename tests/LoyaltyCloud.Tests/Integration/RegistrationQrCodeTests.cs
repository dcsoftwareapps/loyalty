extern alias AdminApp;

using AdminApp::LoyaltyCloud.Admin.Services;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using ZXing;
using ZXing.Common;

namespace LoyaltyCloud.Tests.Integration;

public sealed class RegistrationQrCodeTests
{
    private const string Url = "https://admin.loyaltycloud.net/cafeteria/join";

    [Theory]
    [InlineData(120, 120)] [InlineData(300, 80)] [InlineData(80, 300)]
    public void Logo_preserves_aspect_ratio_with_white_margin_and_decodable_url(int width, int height)
    {
        var bytes = RegistrationQrCodeGenerator.GeneratePng(Url, Logo(width, height));
        using var image = Image.Load<Rgba32>(bytes);
        Assert.Equal(Url, Decode(image));
        var red = new List<Point>();
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                if (image[x, y].R > 200 && image[x, y].G < 60) red.Add(new Point(x, y));
        Assert.NotEmpty(red);
        var actualWidth = red.Max(p => p.X) - red.Min(p => p.X) + 1;
        var actualHeight = red.Max(p => p.Y) - red.Min(p => p.Y) + 1;
        Assert.InRange((double)actualWidth / actualHeight, (double)width / height * .95, (double)width / height * 1.05);
        var box = (int)(image.Width * .18);
        Assert.True(Math.Max(actualWidth, actualHeight) < box);
        Assert.Equal(new Rgba32(255, 255, 255), image[(image.Width - box) / 2, (image.Height - box) / 2]);
        Assert.InRange(Math.Abs((red.Min(p => p.X) + red.Max(p => p.X)) / 2.0 - image.Width / 2.0), 0, 1);
        AssertQuietZone(image);
    }

    [Fact]
    public void Central_alignment_pattern_is_not_covered_by_logo()
    {
        // Version 7 has an alignment pattern at its center.
        var url = "https://admin.loyaltycloud.net/" + new string('a', 25) + "/join";
        using var plain = Image.Load<Rgba32>(RegistrationQrCodeGenerator.GeneratePng(url));
        using var branded = Image.Load<Rgba32>(RegistrationQrCodeGenerator.GeneratePng(url, Logo(100, 100)));
        Assert.Equal(url, Decode(branded));
        Assert.Equal((45 + 8) * 16, plain.Width);
        // Version 7 center = module 22, plus the four-module quiet zone.
        for (var y = 24 * 16; y < 29 * 16; y++)
            for (var x = 24 * 16; x < 29 * 16; x++) Assert.Equal(plain[x, y], branded[x, y]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Missing_or_invalid_logo_keeps_plain_black_white_decodable_qr(bool invalid)
    {
        var bytes = RegistrationQrCodeGenerator.GeneratePng(Url, invalid ? new byte[] { 1, 2, 3 } : null);
        Assert.Equal(RegistrationQrCodeGenerator.GeneratePng(Url), bytes);
        using var image = Image.Load<Rgba32>(bytes);
        Assert.Equal(Url, Decode(image));
        AssertQuietZone(image);
    }

    [Theory]
    [InlineData("https://admin.loyaltycloud.net/a/join")]
    [InlineData("https://admin.loyaltycloud.net/salon-bella-del-mar-2026/join")]
    [InlineData("https://loyaltycloud-admin-stg-01.azurewebsites.net/una-cafeteria-con-nombre-muy-largo-para-alineacion/join")]
    public void Transparent_logo_and_larger_versions_decode_exact_original_url_at_high_ecc(string url)
    {
        using var image = Image.Load<Rgba32>(RegistrationQrCodeGenerator.GeneratePng(url, Logo(80, 80, transparent: true)));
        var result = DecodeResult(image);
        Assert.Equal(url, result.Text);
        Assert.Equal("H", result.ResultMetadata[ResultMetadataType.ERROR_CORRECTION_LEVEL]);
        AssertQuietZone(image);
    }

    [Theory]
    [InlineData(LoyaltyProgramType.Points)] [InlineData(LoyaltyProgramType.Stamps)]
    public async Task Current_tenant_logo_is_reloaded_without_cross_tenant_or_stale_cache(LoyaltyProgramType program)
    {
        var context = new Mock<ITenantContext>();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        context.SetupGet(c => c.TenantId).Returns(tenantA);
        var logos = new Mock<ITenantBrandingLogoService>();
        logos.SetupSequence(s => s.ReadRegistrationLogoAsync(tenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Logo(100, 50)).ReturnsAsync(Logo(50, 100));
        logos.Setup(s => s.ReadRegistrationLogoAsync(tenantB, It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        var service = new RegistrationQrCodeGenerator(context.Object, logos.Object, NullLogger<RegistrationQrCodeGenerator>.Instance);
        var url = $"https://admin.loyaltycloud.net/cafeteria-{program.ToString().ToLowerInvariant()}/join";
        var first = await service.GenerateDataUriAsync(url);
        var updated = await service.GenerateDataUriAsync(url);
        Assert.NotEqual(first, updated);
        using var updatedImage = Image.Load<Rgba32>(Convert.FromBase64String(updated.Split(',')[1]));
        Assert.Equal(url, Decode(updatedImage));
        context.SetupGet(c => c.TenantId).Returns(tenantB);
        var other = await service.GenerateDataUriAsync(url);
        Assert.Equal("data:image/png;base64," + Convert.ToBase64String(RegistrationQrCodeGenerator.GeneratePng(url)), other);
        logos.Verify(s => s.ReadRegistrationLogoAsync(tenantA, It.IsAny<CancellationToken>()), Times.Exactly(2));
        logos.Verify(s => s.ReadRegistrationLogoAsync(tenantB, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unavailable_logo_falls_back_without_failing_the_page()
    {
        var context = new Mock<ITenantContext>();
        context.SetupGet(c => c.TenantId).Returns(Guid.NewGuid());
        var logos = new Mock<ITenantBrandingLogoService>();
        logos.Setup(s => s.ReadRegistrationLogoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Unavailable"));
        var service = new RegistrationQrCodeGenerator(context.Object, logos.Object, NullLogger<RegistrationQrCodeGenerator>.Instance);
        var uri = await service.GenerateDataUriAsync(Url);
        using var image = Image.Load<Rgba32>(Convert.FromBase64String(uri.Split(',')[1]));
        Assert.Equal(Url, Decode(image));
    }

    private static byte[] Logo(int width, int height, bool transparent = false)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(240, 20, 20));
        if (transparent)
            for (var y = 0; y < height / 2; y++)
                for (var x = 0; x < width; x++) image[x, y] = new Rgba32(0, 0, 0, 0);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static void AssertQuietZone(Image<Rgba32> image)
    {
        var white = new Rgba32(255, 255, 255);
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                if (x < 64 || y < 64 || x >= image.Width - 64 || y >= image.Height - 64) Assert.Equal(white, image[x, y]);
    }

    private static string Decode(Image<Rgba32> image) => DecodeResult(image).Text;
    private static Result DecodeResult(Image<Rgba32> image)
    {
        var rgb = new byte[image.Width * image.Height * 3];
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                var i = (y * image.Width + x) * 3;
                rgb[i] = image[x, y].R; rgb[i + 1] = image[x, y].G; rgb[i + 2] = image[x, y].B;
            }
        return new MultiFormatReader().decode(new BinaryBitmap(new HybridBinarizer(
            new RGBLuminanceSource(rgb, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGB24))));
    }
}
