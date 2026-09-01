using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace LoyaltyCloud.Infrastructure.Services;

internal interface IApplePassPackageBuilder
{
    Task<byte[]> BuildAsync(byte[] passJson, IReadOnlyList<WalletPassAsset> assets, CancellationToken ct = default);
}

internal sealed class ApplePassPackageBuilder(IAppleWalletSecretsProvider secrets) : IApplePassPackageBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false, PropertyNamingPolicy = null };

    public async Task<byte[]> BuildAsync(byte[] passJson, IReadOnlyList<WalletPassAsset> assets, CancellationToken ct = default)
    {
        var manifest = new Dictionary<string,string>(StringComparer.Ordinal) { ["pass.json"] = Sha1Hex(passJson) };
        foreach (var asset in assets) manifest[asset.Name] = Sha1Hex(asset.Bytes);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        var signature = await SignAsync(manifestBytes, ct);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(zip,"pass.json",passJson); Add(zip,"manifest.json",manifestBytes); Add(zip,"signature",signature);
            foreach (var asset in assets) Add(zip,asset.Name,asset.Bytes);
        }
        return output.ToArray();
    }

    private async Task<byte[]> SignAsync(byte[] manifest, CancellationToken ct)
    {
        var collection = X509CertificateLoader.LoadPkcs12Collection(
            await secrets.GetPassCertificateBytesAsync(ct),
            await secrets.GetPassCertificatePasswordAsync(ct),
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
        var passCert = collection.OfType<X509Certificate2>().FirstOrDefault(x=>x.HasPrivateKey)
            ?? throw new InvalidOperationException("El .p12 Apple Wallet no contiene llave privada.");
        var wwdr = collection.OfType<X509Certificate2>().FirstOrDefault(IsWwdr) ?? LoadBundledWwdr();
        if (wwdr is null)
        {
            var bytes = await secrets.GetWwdrCertificateBytesAsync(ct);
            if (bytes is not null) wwdr = LoadCertificate(bytes);
        }
        if (wwdr is null || !IsWwdr(wwdr)) throw new InvalidOperationException("No se encontró el certificado Apple WWDR G4.");
        var cms = new SignedCms(new ContentInfo(manifest), detached:true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber,passCert) { IncludeOption=X509IncludeOption.EndCertOnly };
        signer.Certificates.Add(wwdr); signer.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow)); cms.ComputeSignature(signer);
        return cms.Encode();
    }

    private static bool IsWwdr(X509Certificate2 cert) => cert.Subject.Contains("Apple Worldwide Developer Relations",StringComparison.OrdinalIgnoreCase) && (cert.Subject.Contains("G4",StringComparison.OrdinalIgnoreCase)||cert.Issuer.Contains("G4",StringComparison.OrdinalIgnoreCase));
    private static X509Certificate2? LoadBundledWwdr(){var path=Path.Combine(AppContext.BaseDirectory,"Certificates","AppleWWDRCAG4.cer");return File.Exists(path)?X509CertificateLoader.LoadCertificateFromFile(path):null;}
    private static X509Certificate2 LoadCertificate(byte[] bytes){var text=Encoding.ASCII.GetString(bytes);return text.Contains("-----BEGIN CERTIFICATE-----",StringComparison.Ordinal)?X509Certificate2.CreateFromPem(text):X509CertificateLoader.LoadCertificate(bytes);}
    private static string Sha1Hex(byte[] data)=>Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();
    private static void Add(ZipArchive zip,string name,byte[] bytes){var entry=zip.CreateEntry(name,CompressionLevel.Optimal);using var stream=entry.Open();stream.Write(bytes);}
}
