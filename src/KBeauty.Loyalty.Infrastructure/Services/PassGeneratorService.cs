using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Azure.Security.KeyVault.Secrets;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.ValueObjects;
using KBeauty.Loyalty.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KBeauty.Loyalty.Infrastructure.Services;

/// <summary>
/// Genera el archivo <c>.pkpass</c> firmado para Apple Wallet.
/// </summary>
/// <remarks>
/// Flujo:
/// <list type="number">
///   <item>Construye <c>pass.json</c> con campos según nivel.</item>
///   <item>Calcula SHA-1 de pass.json + iconos → <c>manifest.json</c>.</item>
///   <item>Firma manifest.json con PKCS#7 detached usando el cert de Key Vault.</item>
///   <item>Empaqueta todo como ZIP <c>.pkpass</c>.</item>
/// </list>
/// El certificado .p12 NUNCA toca disco — siempre se lee de Key Vault.
/// </remarks>
internal sealed class PassGeneratorService : IPassGeneratorService
{
    // Nombres de secrets esperados en Key Vault.
    private const string SecretPassCertificate = "kbeauty-pass-certificate";
    private const string SecretPassCertificatePassword = "kbeauty-pass-certificate-password";

    private readonly SecretClient _kv;
    private readonly IStorageService _storage;
    private readonly ApplePassOptions _options;
    private readonly ILogger<PassGeneratorService> _logger;

    private static readonly JsonSerializerOptions PassJsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null // dejar nombres tal como están — Apple es estricto
    };

    public PassGeneratorService(
        SecretClient kv,
        IStorageService storage,
        IOptions<ApplePassOptions> options,
        ILogger<PassGeneratorService> logger)
    {
        _kv = kv;
        _storage = storage;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<byte[]> GeneratePassAsync(LoyaltyCard card, Customer customer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(customer);

        // 1. pass.json
        var passJson = BuildPassJson(card, customer);
        var passJsonBytes = JsonSerializer.SerializeToUtf8Bytes(passJson, PassJsonOpts);

        // 2. Iconos (placeholders — el README explica cómo reemplazar en prod)
        var iconBytes = PlaceholderIconPng;
        var icon2xBytes = PlaceholderIconPng;
        var icon3xBytes = PlaceholderIconPng;

        // 3. manifest.json — diccionario nombre → SHA1 hex
        var manifest = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pass.json"] = Sha1Hex(passJsonBytes),
            ["icon.png"] = Sha1Hex(iconBytes),
            ["icon@2x.png"] = Sha1Hex(icon2xBytes),
            ["icon@3x.png"] = Sha1Hex(icon3xBytes)
        };
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, PassJsonOpts);

        // 4. Firma PKCS#7 detached del manifest
        var signatureBytes = await SignManifestAsync(manifestBytes, ct);

        // 5. Empaqueta como ZIP
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipEntry(zip, "pass.json", passJsonBytes);
            AddZipEntry(zip, "manifest.json", manifestBytes);
            AddZipEntry(zip, "signature", signatureBytes);
            AddZipEntry(zip, "icon.png", iconBytes);
            AddZipEntry(zip, "icon@2x.png", icon2xBytes);
            AddZipEntry(zip, "icon@3x.png", icon3xBytes);
        }

        _logger.LogInformation(
            "Pass .pkpass generado para serial {Serial} ({Bytes} bytes)",
            card.SerialNumber, output.Length);

        return output.ToArray();
    }

    /// <inheritdoc />
    public Task<string> GetPassDownloadUrlAsync(string serialNumber, CancellationToken ct = default)
    {
        // Delega al storage — si el blob existe genera SAS, si no devuelve null.
        // Aquí solo retornamos la "promesa" — el caller (controller) decide qué hacer si está vacío.
        return Task.FromResult($"/api/customers/{serialNumber}/pass");
    }

    private object BuildPassJson(LoyaltyCard card, Customer customer)
    {
        var (bg, fg, label) = ColorsForLevel(card.Level);

        return new
        {
            formatVersion = 1,
            passTypeIdentifier = _options.PassTypeIdentifier,
            serialNumber = card.SerialNumber,
            teamIdentifier = _options.TeamIdentifier,
            webServiceURL = _options.WebServiceURL,
            authenticationToken = card.AuthenticationToken,
            organizationName = _options.OrganizationName,
            description = "Tarjeta de Lealtad KBeauty MX",
            logoText = "KBeauty MX",
            backgroundColor = bg,
            foregroundColor = fg,
            labelColor = label,
            storeCard = new
            {
                primaryFields = new[]
                {
                    new
                    {
                        key = "points",
                        label = "PUNTOS",
                        value = card.CurrentPoints,
                        textAlignment = "PKTextAlignmentCenter"
                    }
                },
                secondaryFields = new object[]
                {
                    new { key = "level", label = "NIVEL", value = card.Level },
                    new { key = "earned", label = "ESTE AÑO", value = card.PointsEarnedThisYear }
                },
                auxiliaryFields = new[]
                {
                    new { key = "member", label = "MIEMBRO", value = customer.FullName }
                },
                backFields = new object[]
                {
                    new
                    {
                        key = "about",
                        label = "Programa de Lealtad",
                        value = "Acumula puntos en cada compra y canjéalos por beneficios exclusivos en KBeauty MX. Niveles: Mist · Glow · Radiance."
                    },
                    new
                    {
                        key = "store",
                        label = "Tienda",
                        value = "KBeauty MX — Ensenada, Baja California"
                    },
                    new
                    {
                        key = "web",
                        label = "Sitio web",
                        value = "kbeautymx.com"
                    },
                    new
                    {
                        key = "serial",
                        label = "Serial",
                        value = card.SerialNumber
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
                    altText = card.SerialNumber
                }
            }
        };
    }

    private static (string bg, string fg, string label) ColorsForLevel(string level) =>
        string.Equals(level, LoyaltyConstants.Levels.Radiance, StringComparison.Ordinal)
            ? ("rgb(44, 24, 16)", "rgb(247, 245, 240)", "rgb(184, 152, 106)")
            : ("rgb(247, 245, 240)", "rgb(28, 27, 24)", "rgb(155, 152, 136)");

    private async Task<byte[]> SignManifestAsync(byte[] manifestBytes, CancellationToken ct)
    {
        var certB64 = (await _kv.GetSecretAsync(SecretPassCertificate, cancellationToken: ct)).Value.Value;
        var certPassword = (await _kv.GetSecretAsync(SecretPassCertificatePassword, cancellationToken: ct)).Value.Value;

        var certBytes = Convert.FromBase64String(certB64);

        // EphemeralKeySet = no persiste la llave en el almacén del SO — más seguro en App Service.
        using var cert = X509CertificateLoader.LoadPkcs12(
            certBytes,
            certPassword,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

        var contentInfo = new ContentInfo(manifestBytes);
        var cms = new SignedCms(contentInfo, detached: true);

        var signer = new CmsSigner(cert)
        {
            IncludeOption = X509IncludeOption.WholeChain
        };

        cms.ComputeSignature(signer);
        return cms.Encode();
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

    /// <summary>
    /// PNG 1×1 transparente — placeholder. En producción reemplazar por iconos
    /// reales (29×29, 58×58, 87×87 px respectivamente) — el README explica cómo.
    /// </summary>
    private static readonly byte[] PlaceholderIconPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xFA, 0xCF, 0x00, 0x00,
        0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC, 0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
        0xAE, 0x42, 0x60, 0x82
    };
}
