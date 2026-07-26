using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantBrandingLogoService :
    ITenantBrandingLogoService,
    ITenantBrandingLogoUrlProvider
{
    public const long MaxLogoBytes = 2 * 1024 * 1024;
    private const string OriginalBlobName = "logo-original";
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg"
    };

    private static readonly WalletLogoAssetSpec[] WalletAssets =
    [
        new("logo.png", 160, 50, TransparentCanvas: true),
        new("logo@2x.png", 320, 100, TransparentCanvas: true),
        new("logo@3x.png", 480, 150, TransparentCanvas: true),
        new("icon.png", 29, 29, TransparentCanvas: false),
        new("icon@2x.png", 58, 58, TransparentCanvas: false),
        new("icon@3x.png", 87, 87, TransparentCanvas: false)
    ];

    private readonly AppDbContext _db;
    private readonly AzureStorageOptions _options;
    private readonly BlobContainerClient? _container;
    private readonly ILogger<TenantBrandingLogoService> _logger;

    public TenantBrandingLogoService(
        AppDbContext db,
        IOptions<AzureStorageOptions> options,
        ILogger<TenantBrandingLogoService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            var service = new BlobServiceClient(_options.ConnectionString);
            _container = service.GetBlobContainerClient(_options.PassContainer);
        }
    }

    public async Task<Result<TenantBrandingLogoResult>> UploadAsync(
        Guid tenantId,
        string fileName,
        string contentType,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result.Fail<TenantBrandingLogoResult>("TenantId requerido.");
        if (contentLength <= 0 || contentLength > MaxLogoBytes)
            return Result.Fail<TenantBrandingLogoResult>("El logo debe pesar maximo 2 MB.");
        if (!AllowedContentTypes.Contains(contentType))
            return Result.Fail<TenantBrandingLogoResult>("El logo debe ser PNG o JPG.");
        if (_container is null)
            return Result.Fail<TenantBrandingLogoResult>("Azure Blob Storage no esta configurado para subir logos.");

        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var branding = await _db.TenantBrandings
            .SingleOrDefaultAsync(b => b.TenantId == tenantId, cancellationToken);
        if (branding is null)
            return Result.Fail<TenantBrandingLogoResult>("Branding del tenant no encontrado.");

        byte[] originalBytes;
        await using (var ms = new MemoryStream())
        {
            await content.CopyToAsync(ms, cancellationToken);
            originalBytes = ms.ToArray();
        }

        if (originalBytes.LongLength != contentLength)
            _logger.LogDebug(
                "Tenant logo stream length differed from browser metadata. TenantId={TenantId}, MetadataLength={MetadataLength}, ActualLength={ActualLength}.",
                tenantId,
                contentLength,
                originalBytes.LongLength);

        Image<Rgba32> original;
        try
        {
            original = Image.Load<Rgba32>(originalBytes);
        }
        catch
        {
            return Result.Fail<TenantBrandingLogoResult>("El archivo no es una imagen valida.");
        }

        using (original)
        {
            var extension = GetSafeExtension(fileName, contentType);
            var originalBlobName = $"{GetTenantBrandingPrefix(tenantId)}/{OriginalBlobName}{extension}";
            await UploadPngOrOriginalAsync(originalBlobName, originalBytes, contentType, cancellationToken);

            foreach (var spec in WalletAssets)
            {
                var bytes = RenderPng(original, spec);
                await UploadPngOrOriginalAsync(
                    $"{GetTenantBrandingPrefix(tenantId)}/wallet/{spec.Name}",
                    bytes,
                    "image/png",
                    cancellationToken);
            }

            branding.SetLogo(null, originalBlobName);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Tenant logo uploaded. TenantId={TenantId}, OriginalBlob={OriginalBlob}, WalletAssets={WalletAssetCount}.",
                tenantId,
                originalBlobName,
                WalletAssets.Length);

            return Result.Ok(new TenantBrandingLogoResult(
                tenantId,
                originalBlobName,
                GetDisplayUrl(originalBlobName)));
        }
    }

    public async Task<Result> RemoveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result.Fail("TenantId requerido.");

        var branding = await _db.TenantBrandings
            .SingleOrDefaultAsync(b => b.TenantId == tenantId, cancellationToken);
        if (branding is null)
            return Result.Fail("Branding del tenant no encontrado.");

        branding.ClearLogo();
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tenant logo reference removed. TenantId={TenantId}.", tenantId);
        return Result.Ok();
    }

    public string? GetDisplayUrl(string? logoBlobName)
    {
        if (_container is null || string.IsNullOrWhiteSpace(logoBlobName))
            return null;

        var blob = _container.GetBlobClient(logoBlobName);
        if (!blob.CanGenerateSasUri)
            return blob.Uri.ToString();

        return blob.GenerateSasUri(
            BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.AddMinutes(Math.Max(5, _options.SasExpirationMinutes))).ToString();
    }

    internal static string GetTenantBrandingPrefix(Guid tenantId) =>
        $"tenant-branding/{tenantId:D}";

    private async Task UploadPngOrOriginalAsync(
        string blobName,
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        var blob = _container!.GetBlobClient(blobName);
        using var stream = new MemoryStream(bytes);
        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        await blob.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    CacheControl = "no-cache"
                }
            },
            cancellationToken);
    }

    private static byte[] RenderPng(Image<Rgba32> original, WalletLogoAssetSpec spec)
    {
        using var canvas = new Image<Rgba32>(
            spec.Width,
            spec.Height,
            spec.TransparentCanvas ? Color.Transparent : Color.White);
        using var resized = original.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(spec.Width, spec.Height),
            Mode = ResizeMode.Max
        }));

        var x = (spec.Width - resized.Width) / 2;
        var y = (spec.Height - resized.Height) / 2;
        canvas.Mutate(ctx => ctx.DrawImage(resized, new Point(x, y), 1f));

        using var ms = new MemoryStream();
        canvas.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static string GetSafeExtension(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            return ".png";
        if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return ".jpg";
        }

        return contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
    }

    private sealed record WalletLogoAssetSpec(
        string Name,
        int Width,
        int Height,
        bool TransparentCanvas);
}
