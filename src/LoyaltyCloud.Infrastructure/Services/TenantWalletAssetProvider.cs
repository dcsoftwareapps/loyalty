using Azure;
using Azure.Storage.Blobs;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantWalletAssetProvider : ITenantWalletAssetProvider
{
    private static readonly WalletAssetSpec[] RequiredAssets =
    [
        new("icon.png", 29, 29),
        new("icon@2x.png", 58, 58),
        new("icon@3x.png", 87, 87),
        new("logo.png", 160, 50),
        new("logo@2x.png", 320, 100),
        new("logo@3x.png", 480, 150)
    ];

    private readonly AzureStorageOptions _options;
    private readonly ILogger<TenantWalletAssetProvider> _logger;

    public TenantWalletAssetProvider(
        IOptions<AzureStorageOptions> options,
        ILogger<TenantWalletAssetProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WalletPassAsset>> LoadAssetsAsync(
        string tenantSlug,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = string.IsNullOrWhiteSpace(tenantSlug)
            ? "unknown"
            : tenantSlug.Trim().ToLowerInvariant();

        var tenantAssets = await TryLoadTenantBlobAssetsAsync(normalizedSlug, cancellationToken);
        if (tenantAssets is not null)
            return tenantAssets;

        if (string.Equals(normalizedSlug, TenantSeed.KBeautySlug, StringComparison.Ordinal))
            return LoadLocalAssets(Path.Combine(AppContext.BaseDirectory, "Assets", "AppleWallet"), "legacy-kbeauty", normalizedSlug);

        return LoadLocalAssets(Path.Combine(AppContext.BaseDirectory, "Assets", "AppleWalletGeneric"), "generic-loyaltycloud", normalizedSlug);
    }

    private async Task<IReadOnlyList<WalletPassAsset>?> TryLoadTenantBlobAssetsAsync(
        string tenantSlug,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            return null;

        try
        {
            var service = new BlobServiceClient(_options.ConnectionString);
            var container = service.GetBlobContainerClient(_options.PassContainer);
            var loaded = new List<WalletPassAsset>(RequiredAssets.Length);

            foreach (var spec in RequiredAssets)
            {
                var blobName = $"tenants/{tenantSlug}/branding/wallet/{spec.Name}";
                var blob = container.GetBlobClient(blobName);
                if (!await blob.ExistsAsync(cancellationToken))
                {
                    if (loaded.Count > 0)
                    {
                        _logger.LogWarning(
                            "Tenant wallet assets are incomplete. TenantSlug={TenantSlug}, MissingBlob={BlobName}; falling back to bundled assets.",
                            tenantSlug,
                            blobName);
                    }

                    return null;
                }

                using var ms = new MemoryStream();
                await blob.DownloadToAsync(ms, cancellationToken);
                var bytes = ms.ToArray();
                ValidatePngDimensions(bytes, spec, blobName);
                loaded.Add(new WalletPassAsset(spec.Name, bytes));
            }

            _logger.LogInformation(
                "Loaded tenant wallet assets from Blob Storage. TenantSlug={TenantSlug}, Files={Files}",
                tenantSlug,
                string.Join(", ", loaded.Select(a => a.Name)));
            return loaded;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogDebug(
                ex,
                "Tenant wallet assets could not be loaded from Blob Storage. TenantSlug={TenantSlug}; falling back to bundled assets.",
                tenantSlug);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogDebug(
                ex,
                "Tenant wallet asset stream failed. TenantSlug={TenantSlug}; falling back to bundled assets.",
                tenantSlug);
            return null;
        }
    }

    private IReadOnlyList<WalletPassAsset> LoadLocalAssets(string assetsDir, string source, string tenantSlug)
    {
        var assets = new List<WalletPassAsset>(RequiredAssets.Length);
        foreach (var spec in RequiredAssets)
        {
            var path = Path.Combine(assetsDir, spec.Name);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Asset requerido para Apple Wallet no existe. Ruta esperada: {path}.",
                    path);

            var bytes = File.ReadAllBytes(path);
            ValidatePngDimensions(bytes, spec, path);
            _logger.LogInformation(
                "Asset Apple Wallet agregado: {File} ({Width}x{Height}, {Bytes} bytes, Source={Source}, TenantSlug={TenantSlug})",
                spec.Name,
                spec.Width,
                spec.Height,
                bytes.Length,
                source,
                tenantSlug);

            assets.Add(new WalletPassAsset(spec.Name, bytes));
        }

        return assets;
    }

    private static void ValidatePngDimensions(byte[] bytes, WalletAssetSpec spec, string path)
    {
        var (width, height) = ReadPngDimensions(bytes, path);
        if (width != spec.Width || height != spec.Height)
            throw new InvalidOperationException(
                $"Asset Apple Wallet '{path}' tiene dimensiones {width}x{height}; se esperaba {spec.Width}x{spec.Height}.");
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

    private sealed record WalletAssetSpec(string Name, int Width, int Height);
}
