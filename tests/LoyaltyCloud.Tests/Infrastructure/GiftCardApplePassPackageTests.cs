using System.IO.Compression;
using System.Text.Json;
using LoyaltyCloud.Infrastructure.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GiftCardApplePassPackageTests
{
    [Fact]
    public async Task DevelopmentPackage_ContainsPassManifestAndAssets()
    {
        var passJson = JsonSerializer.SerializeToUtf8Bytes(new { description = "Gift Card", storeCard = new { }, balance = "750.00 MXN" });
        var bytes = await new DevelopmentApplePassPackageBuilder().BuildAsync(passJson, []);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("pass.json"));
        Assert.NotNull(archive.GetEntry("manifest.json"));
        Assert.NotNull(archive.GetEntry("icon.png"));
        using var reader = new StreamReader(archive.GetEntry("pass.json")!.Open());
        var json = await reader.ReadToEndAsync();
        Assert.Contains("Gift Card", json);
        Assert.Contains("750.00 MXN", json);
    }

    [Fact]
    public async Task DevelopmentPackage_PreservesTenantSpecificPassJson()
    {
        var builder = new DevelopmentApplePassPackageBuilder();
        var a = await ReadPassAsync(await builder.BuildAsync(JsonSerializer.SerializeToUtf8Bytes(new { organizationName = "Tenant A", backgroundColor = "#111111" }), []));
        var b = await ReadPassAsync(await builder.BuildAsync(JsonSerializer.SerializeToUtf8Bytes(new { organizationName = "Tenant B", backgroundColor = "#224466" }), []));

        Assert.Contains("Tenant A", a);
        Assert.DoesNotContain("Tenant A", b);
        Assert.Contains("#224466", b);
    }

    private static async Task<string> ReadPassAsync(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("pass.json")!.Open());
        return await reader.ReadToEndAsync();
    }
}
