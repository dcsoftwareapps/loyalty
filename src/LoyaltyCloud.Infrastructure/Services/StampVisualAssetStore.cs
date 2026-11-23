using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class StampVisualAssetStore(IOptions<AzureStorageOptions> options) : IStampVisualAssetStore
{
    private BlobContainerClient Container => string.IsNullOrWhiteSpace(options.Value.ConnectionString)
        ? throw new InvalidOperationException("Azure Blob Storage no está configurado.")
        : new BlobServiceClient(options.Value.ConnectionString).GetBlobContainerClient(options.Value.PassContainer);
    private static string Prefix(Guid tenantId) => $"{TenantBrandingLogoService.GetTenantBrandingPrefix(tenantId)}/stamps/";

    public async Task<string> UploadAsync(Guid tenantId, byte[] png, CancellationToken ct)
    {
        var container = Container;
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        var name = $"{Prefix(tenantId)}{Guid.NewGuid():N}.png";
        using var stream = new MemoryStream(png, writable: false);
        await container.GetBlobClient(name).UploadAsync(stream,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "image/png" } }, ct);
        return name;
    }

    public async Task<byte[]?> ReadAsync(Guid tenantId, string? blobName, CancellationToken ct)
    {
        if (blobName is null) return null;
        if (!blobName.StartsWith(Prefix(tenantId), StringComparison.Ordinal)
            || blobName.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Cross-tenant Stamps asset path.");
        try
        {
            var response = await Container.GetBlobClient(blobName).DownloadContentAsync(ct);
            return response.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }
}
