using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KBeauty.Loyalty.Infrastructure.Services;

/// <summary>Genera el archivo .pkpass firmado para Apple Wallet.</summary>
internal sealed class PassGeneratorService : IPassGeneratorService
{
    private readonly IAppleWalletSecretsProvider _secrets;
    private readonly ApplePassOptions _options;
    private readonly ILogger<PassGeneratorService> _logger;

    private static readonly JsonSerializerOptions PassJsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null
    };

    private static readonly PassAssetSpec[] PassAssetSpecs =
    [
        new("icon.png", 29, 29),
        new("icon@2x.png", 58, 58),
        new("icon@3x.png", 87, 87),
        new("logo.png", 160, 50),
        new("logo@2x.png", 320, 100),
        new("logo@3x.png", 480, 150)
    ];

    public PassGeneratorService(
        IAppleWalletSecretsProvider secrets,
        IOptions<ApplePassOptions> options,
        ILogger<PassGeneratorService> logger)
    {
        _secrets = secrets;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<byte[]> GeneratePassAsync(LoyaltyCard card, Customer customer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(customer);

        var passJson = BuildPassJson(card, customer);
        var passJsonBytes = JsonSerializer.SerializeToUtf8Bytes(passJson, PassJsonOpts);
        var assets = LoadPassAssets();

        var manifest = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pass.json"] = Sha1Hex(passJsonBytes)
        };

        foreach (var asset in assets)
            manifest[asset.Name] = Sha1Hex(asset.Bytes);

        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, PassJsonOpts);
        var signatureBytes = await SignManifestAsync(manifestBytes, ct);

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipEntry(zip, "pass.json", passJsonBytes);
            AddZipEntry(zip, "manifest.json", manifestBytes);
            AddZipEntry(zip, "signature", signatureBytes);

            foreach (var asset in assets)
                AddZipEntry(zip, asset.Name, asset.Bytes);
        }

        _logger.LogInformation(
            "Pass .pkpass generado para serial {Serial} ({Bytes} bytes). Archivos: {Files}",
            card.SerialNumber,
            output.Length,
            string.Join(", ", new[] { "pass.json", "manifest.json", "signature" }.Concat(assets.Select(a => a.Name))));

        return output.ToArray();
    }

    public Task<string> GetPassDownloadUrlAsync(string serialNumber, CancellationToken ct = default) =>
        Task.FromResult($"/api/customers/{serialNumber}/pass");

    private object BuildPassJson(LoyaltyCard card, Customer customer)
    {
        EnsureRequiredOption(_options.PassTypeIdentifier, "Apple:PassTypeIdentifier");
        EnsureRequiredOption(_options.TeamIdentifier, "Apple:TeamIdentifier");
        EnsureRequiredOption(_options.WebServiceURL, "Apple:WebServiceURL");
        EnsureRequiredOption(_options.OrganizationName, "Apple:OrganizationName");

        var progress = BuildLevelProgress(card);
        var displayName = GetWalletDisplayName(customer);

        return new
        {
            formatVersion = 1,
            passTypeIdentifier = _options.PassTypeIdentifier,
            serialNumber = card.SerialNumber,
            teamIdentifier = _options.TeamIdentifier,
            webServiceURL = _options.WebServiceURL,
            authenticationToken = card.AuthenticationToken,
            organizationName = _options.OrganizationName,
            description = "Tarjeta de Lealtad K-Beauty",
            backgroundColor = "rgb(250,248,244)",
            foregroundColor = "rgb(28,28,28)",
            labelColor = "rgb(132,124,120)",
            storeCard = new
            {
                primaryFields = new[]
                {
                    new
                    {
                        key = "name",
                        label = string.Empty,
                        value = displayName,
                        textAlignment = "PKTextAlignmentCenter"
                    }
                },
                secondaryFields = Array.Empty<object>(),
                auxiliaryFields = new[]
                {
                    new { key = "points", label = "PUNTOS", value = progress.PointsText },
                    new { key = "level", label = "NIVEL", value = progress.LevelShortText },
                    new { key = "next", label = "PR\u00d3XIMO", value = progress.NextLevelText }
                },
                backFields = new object[]
                {
                    new
                    {
                        key = "benefits",
                        label = "Beneficios",
                        value = "\u2022 Acumula puntos en cada compra.\n\n\u2022 Desbloquea recompensas exclusivas.\n\n\u2022 Accede a beneficios seg\u00fan tu nivel."
                    },
                    new
                    {
                        key = "progress",
                        label = "Progreso",
                        value = $"Nivel actual\n{progress.LevelShortText}\n\nPr\u00f3ximo nivel\n{progress.NextLevelText}\n\nPuntos restantes\n{progress.RemainingPointsText}"
                    },
                    new
                    {
                        key = "contact",
                        label = string.Empty,
                        value = "@kbeauty_mx\n\nkbeautymx.com\n\n+52 646 238 6962"
                    }
                }
            },
            barcodes = new[]
            {
                new
                {
                    format = "PKBarcodeFormatQR",
                    message = card.SerialNumber,
                    messageEncoding = "iso-8859-1",
                    altText = "Presenta este c\u00f3digo en caja"
                }
            }
        };
    }

    private static string GetWalletDisplayName(Customer customer)
    {
        var fullName = customer.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return "Cliente K-Beauty";

        var firstName = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstName)
            ? "Cliente K-Beauty"
            : firstName;
    }

    private static PassProgress BuildLevelProgress(LoyaltyCard card)
    {
        var currentPoints = Math.Max(0, card.CurrentPoints);
        var level = card.Level;
        var levelDisplay = $"{level} Member \u2728";
        var levelShortText = $"{level} \u2728";

        if (string.Equals(level, LoyaltyConstants.Levels.Radiance, StringComparison.Ordinal) ||
            currentPoints >= LoyaltyConstants.Defaults.LevelRadianceMin)
        {
            return new PassProgress(
                levelDisplay,
                levelShortText,
                $"{currentPoints} pts",
                "\u2b50 M\u00e1ximo",
                "0 pts");
        }

        var nextLevel = string.Equals(level, LoyaltyConstants.Levels.Glow, StringComparison.Ordinal)
            ? LoyaltyConstants.Levels.Radiance
            : LoyaltyConstants.Levels.Glow;
        var targetPoints = string.Equals(nextLevel, LoyaltyConstants.Levels.Radiance, StringComparison.Ordinal)
            ? LoyaltyConstants.Defaults.LevelRadianceMin
            : LoyaltyConstants.Defaults.LevelGlowMin;
        var remainingPoints = Math.Max(0, targetPoints - currentPoints);

        return new PassProgress(
            levelDisplay,
            levelShortText,
            $"{currentPoints} pts",
            nextLevel,
            $"{remainingPoints} pts");
    }

    private IReadOnlyList<PassAsset> LoadPassAssets()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets", "AppleWallet");
        var assets = new List<PassAsset>(PassAssetSpecs.Length);

        foreach (var spec in PassAssetSpecs)
        {
            var path = Path.Combine(assetsDir, spec.Name);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Asset requerido para Apple Wallet no existe. Ruta esperada: {path}. " +
                    "Regeneralo con scripts/generate-wallet-assets.ps1.",
                    path);

            var bytes = File.ReadAllBytes(path);
            var (width, height) = ReadPngDimensions(bytes, path);

            if (width != spec.Width || height != spec.Height)
                throw new InvalidOperationException(
                    $"Asset Apple Wallet '{path}' tiene dimensiones {width}x{height}; " +
                    $"se esperaba {spec.Width}x{spec.Height}. " +
                    "Regeneralo con scripts/generate-wallet-assets.ps1.");

            _logger.LogInformation(
                "Asset Apple Wallet agregado: {File} ({Width}x{Height}, {Bytes} bytes)",
                spec.Name,
                width,
                height,
                bytes.Length);

            assets.Add(new PassAsset(spec.Name, bytes));
        }

        return assets;
    }

    private async Task<byte[]> SignManifestAsync(byte[] manifestBytes, CancellationToken ct)
    {
        var certBytes = await _secrets.GetPassCertificateBytesAsync(ct);
        var certPassword = await _secrets.GetPassCertificatePasswordAsync(ct);

        var certCollection = X509CertificateLoader.LoadPkcs12Collection(
            certBytes,
            certPassword,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

        var passCert = certCollection
            .OfType<X509Certificate2>()
            .FirstOrDefault(c => c.HasPrivateKey)
            ?? throw new InvalidOperationException(
                "El .p12 de Apple Wallet no contiene un certificado con llave privada.");

        var wwdrCert = certCollection
            .OfType<X509Certificate2>()
            .FirstOrDefault(IsWwdrG4Certificate);

        wwdrCert ??= LoadBundledWwdrCertificate();

        if (wwdrCert is null)
        {
            var wwdrBytes = await _secrets.GetWwdrCertificateBytesAsync(ct);
            if (wwdrBytes is not null)
                wwdrCert = LoadCertificate(wwdrBytes);
        }

        if (wwdrCert is null || !IsWwdrG4Certificate(wwdrCert))
        {
            throw new InvalidOperationException(
                "No se encontro el certificado intermedio Apple WWDR G4. " +
                "Incluyelo dentro del .p12 o configura Apple:WwdrCertificatePath con el certificado WWDR G4.");
        }

        var contentInfo = new ContentInfo(manifestBytes);
        var cms = new SignedCms(contentInfo, detached: true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, passCert)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        };
        signer.Certificates.Add(wwdrCert);
        signer.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow));

        cms.ComputeSignature(signer);
        var signatureBytes = cms.Encode();

        LogSignatureDebug(signatureBytes, passCert, wwdrCert);
        return signatureBytes;
    }

    private void LogSignatureDebug(
        byte[] signatureBytes,
        X509Certificate2 passCert,
        X509Certificate2 wwdrCert)
    {
        var signedCms = new SignedCms();
        signedCms.Decode(signatureBytes);

        var signerInfo = signedCms.SignerInfos[0];
        var signedAttributeOids = signerInfo.SignedAttributes
            .Cast<CryptographicAttributeObject>()
            .Select(a => $"{a.Oid?.FriendlyName ?? "unknown"} ({a.Oid?.Value})")
            .ToArray();

        var hasContentType = HasSignedAttribute(signerInfo, "1.2.840.113549.1.9.3");
        var hasMessageDigest = HasSignedAttribute(signerInfo, "1.2.840.113549.1.9.4");
        var hasSigningTime = HasSignedAttribute(signerInfo, "1.2.840.113549.1.9.5");

        _logger.LogInformation(
            "Firma PKCS#7 Apple Wallet generada. Detached={Detached}, PassCert='{PassSubject}', WWDR='{WwdrSubject}', CertsIncluidos={CertificateCount}, SignedAttributes=[{Attributes}]",
            true,
            passCert.Subject,
            wwdrCert.Subject,
            signedCms.Certificates.Count,
            string.Join(", ", signedAttributeOids));

        _logger.LogDebug(
            "Firma PKCS#7 signed attributes: contentType={ContentType}, messageDigest={MessageDigest}, signingTime={SigningTime}",
            hasContentType,
            hasMessageDigest,
            hasSigningTime);
    }

    private static bool HasSignedAttribute(SignerInfo signerInfo, string oid) =>
        signerInfo.SignedAttributes
            .Cast<CryptographicAttributeObject>()
            .Any(a => string.Equals(a.Oid?.Value, oid, StringComparison.Ordinal));

    private static X509Certificate2 LoadCertificate(byte[] bytes)
    {
        var text = Encoding.ASCII.GetString(bytes);
        return text.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal)
            ? X509Certificate2.CreateFromPem(text)
            : X509CertificateLoader.LoadCertificate(bytes);
    }

    private static X509Certificate2? LoadBundledWwdrCertificate()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Certificates", "AppleWWDRCAG4.cer");
        return File.Exists(path)
            ? X509CertificateLoader.LoadCertificateFromFile(path)
            : null;
    }

    private static bool IsWwdrG4Certificate(X509Certificate2 cert) =>
        cert.Subject.Contains("Apple Worldwide Developer Relations", StringComparison.OrdinalIgnoreCase) &&
        (cert.Subject.Contains("G4", StringComparison.OrdinalIgnoreCase) ||
         cert.Issuer.Contains("G4", StringComparison.OrdinalIgnoreCase));

    private static void EnsureRequiredOption(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Falta configuracion requerida '{key}' para generar pases Apple Wallet reales.");
    }

    private static string Sha1Hex(byte[] data)
    {
        var hash = SHA1.HashData(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.AppendFormat("{0:x2}", b);
        return sb.ToString();
    }

    private static void AddZipEntry(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static (int Width, int Height) ReadPngDimensions(byte[] bytes, string path)
    {
        if (bytes.Length < 24 ||
            bytes[0] != 0x89 ||
            bytes[1] != 0x50 ||
            bytes[2] != 0x4E ||
            bytes[3] != 0x47)
        {
            throw new InvalidOperationException($"Asset Apple Wallet '{path}' no es un PNG valido.");
        }

        return (ReadBigEndianInt32(bytes, 16), ReadBigEndianInt32(bytes, 20));
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) |
        (bytes[offset + 1] << 16) |
        (bytes[offset + 2] << 8) |
        bytes[offset + 3];

    private sealed record PassAsset(string Name, byte[] Bytes);

    private sealed record PassAssetSpec(string Name, int Width, int Height);

    private sealed record PassProgress(
        string LevelDisplay,
        string LevelShortText,
        string PointsText,
        string NextLevelText,
        string RemainingPointsText);
}
