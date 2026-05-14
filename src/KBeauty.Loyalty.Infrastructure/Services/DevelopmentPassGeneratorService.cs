using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Infrastructure.Services;

/// <summary>
/// Development-only pass generator. Produces a valid ZIP-shaped .pkpass payload
/// without Apple signing, Key Vault, or production certificates.
/// </summary>
internal sealed class DevelopmentPassGeneratorService : IPassGeneratorService
{
    private readonly ILogger<DevelopmentPassGeneratorService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null
    };

    public DevelopmentPassGeneratorService(ILogger<DevelopmentPassGeneratorService> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> GeneratePassAsync(
        LoyaltyCard card,
        Customer customer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(customer);

        var passJsonBytes = JsonSerializer.SerializeToUtf8Bytes(BuildPassJson(card, customer), JsonOptions);
        var readmeBytes = Encoding.UTF8.GetBytes(
            "KBeauty development mock pass.\n" +
            "This .pkpass is intentionally unsigned and is not valid for Apple Wallet production use.\n");

        var manifest = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pass.json"] = Sha1Hex(passJsonBytes),
            ["README-dev.txt"] = Sha1Hex(readmeBytes),
            ["icon.png"] = Sha1Hex(PlaceholderIconPng),
            ["icon@2x.png"] = Sha1Hex(PlaceholderIconPng),
            ["icon@3x.png"] = Sha1Hex(PlaceholderIconPng)
        };
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipEntry(zip, "pass.json", passJsonBytes);
            AddZipEntry(zip, "manifest.json", manifestBytes);
            AddZipEntry(zip, "README-dev.txt", readmeBytes);
            AddZipEntry(zip, "icon.png", PlaceholderIconPng);
            AddZipEntry(zip, "icon@2x.png", PlaceholderIconPng);
            AddZipEntry(zip, "icon@3x.png", PlaceholderIconPng);
        }

        _logger.LogInformation(
            "Development mock .pkpass generated for serial {Serial} ({Bytes} bytes)",
            card.SerialNumber,
            output.Length);

        return Task.FromResult(output.ToArray());
    }

    public Task<string> GetPassDownloadUrlAsync(string serialNumber, CancellationToken ct = default) =>
        Task.FromResult($"/api/customers/{serialNumber}/pass");

    private static object BuildPassJson(LoyaltyCard card, Customer customer)
    {
        var (bg, fg, label) = string.Equals(card.Level, LoyaltyConstants.Levels.Radiance, StringComparison.Ordinal)
            ? ("rgb(44, 24, 16)", "rgb(247, 245, 240)", "rgb(184, 152, 106)")
            : ("rgb(247, 245, 240)", "rgb(28, 27, 24)", "rgb(155, 152, 136)");

        return new
        {
            mock = true,
            formatVersion = 1,
            passTypeIdentifier = "pass.com.kbeautymx.loyalty.dev",
            serialNumber = card.SerialNumber,
            teamIdentifier = "DEVTEAM",
            organizationName = "KBeauty MX",
            description = "Development mock loyalty pass",
            logoText = "KBeauty MX Dev",
            backgroundColor = bg,
            foregroundColor = fg,
            labelColor = label,
            storeCard = new
            {
                primaryFields = new[]
                {
                    new { key = "points", label = "PUNTOS", value = card.CurrentPoints }
                },
                secondaryFields = new[]
                {
                    new { key = "level", label = "NIVEL", value = card.Level }
                },
                auxiliaryFields = new[]
                {
                    new { key = "member", label = "MIEMBRO", value = customer.FullName }
                },
                backFields = new[]
                {
                    new
                    {
                        key = "dev",
                        label = "Development",
                        value = "Unsigned mock pass generated locally without Key Vault or Apple certificates."
                    },
                    new { key = "serial", label = "Serial", value = card.SerialNumber }
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

    private static void AddZipEntry(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static string Sha1Hex(byte[] data)
    {
        var hash = SHA1.HashData(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.AppendFormat("{0:x2}", b);
        return sb.ToString();
    }

    private static readonly byte[] PlaceholderIconPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xFA, 0xCF, 0x00, 0x00,
        0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC, 0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
        0xAE, 0x42, 0x60, 0x82
    };
}
