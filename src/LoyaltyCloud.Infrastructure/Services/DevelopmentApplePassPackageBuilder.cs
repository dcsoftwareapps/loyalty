using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class DevelopmentApplePassPackageBuilder : IApplePassPackageBuilder
{
    private static readonly byte[] Icon = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAF/gL+3Wj6WQAAAABJRU5ErkJggg==");
    public Task<byte[]> BuildAsync(byte[] passJson, IReadOnlyList<WalletPassAsset> assets, CancellationToken ct = default)
    {
        var all = assets.Count == 0 ? new[] { new WalletPassAsset("icon.png",Icon), new WalletPassAsset("icon@2x.png",Icon) } : assets;
        var manifest = new Dictionary<string,string> { ["pass.json"]=Hash(passJson) };
        foreach(var asset in all) manifest[asset.Name]=Hash(asset.Bytes);
        var manifestBytes=JsonSerializer.SerializeToUtf8Bytes(manifest);
        using var output=new MemoryStream(); using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){Add(zip,"pass.json",passJson);Add(zip,"manifest.json",manifestBytes);foreach(var asset in all)Add(zip,asset.Name,asset.Bytes);} return Task.FromResult(output.ToArray());
    }
    private static string Hash(byte[] value)=>Convert.ToHexString(SHA1.HashData(value)).ToLowerInvariant();
    private static void Add(ZipArchive zip,string name,byte[] value){var entry=zip.CreateEntry(name);using var stream=entry.Open();stream.Write(value);}
}
