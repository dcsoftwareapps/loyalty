using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services;

/// <summary>
/// Almacena archivos <c>.pkpass</c> en Azure Blob Storage y genera SAS URLs
/// temporales para que la clienta descargue su pase.
/// </summary>
/// <remarks>
/// Para local dev: usar Azurite y connection string "UseDevelopmentStorage=true".
/// Para prod con managed identity, GenerateSasUri requiere user-delegation SAS
/// (no implementado aquí — el connection string con account key es el path de MVP).
/// </remarks>
internal sealed class BlobStorageService : IStorageService
{
    private readonly BlobContainerClient _container;
    private readonly AzureStorageOptions _options;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IOptions<AzureStorageOptions> options, ILogger<BlobStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException(
                "Falta Azure:BlobStorage:ConnectionString en configuración.");

        var serviceClient = new BlobServiceClient(_options.ConnectionString);
        _container = serviceClient.GetBlobContainerClient(_options.PassContainer);

        // Idempotente — crea el contenedor si no existe.
        _container.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<string> UploadPassAsync(string serialNumber, byte[] passBytes, CancellationToken ct = default)
    {
        var blobName = $"{serialNumber}.pkpass";
        var blob = _container.GetBlobClient(blobName);

        using var stream = new MemoryStream(passBytes);
        await blob.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = LoyaltyConstants.ApplePass.ContentType }
            },
            ct);

        _logger.LogInformation("Pass uploaded for {Serial} ({Bytes} bytes)", serialNumber, passBytes.Length);

        // SAS URL de descarga con expiración corta.
        if (!blob.CanGenerateSasUri)
        {
            _logger.LogWarning(
                "BlobClient no puede generar SAS — devolviendo URL pública directa. " +
                "Usa connection string con AccountKey o user-delegation SAS para prod.");
            return blob.Uri.ToString();
        }

        var sasUri = blob.GenerateSasUri(
            BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.AddMinutes(_options.SasExpirationMinutes));

        return sasUri.ToString();
    }

    public async Task<byte[]?> DownloadPassAsync(string serialNumber, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient($"{serialNumber}.pkpass");
        if (!await blob.ExistsAsync(ct)) return null;

        using var ms = new MemoryStream();
        await blob.DownloadToAsync(ms, ct);
        return ms.ToArray();
    }
}
