namespace LoyaltyCloud.Application.Common.Interfaces;

/// <summary>
/// Almacenamiento de los archivos <c>.pkpass</c> generados (Azure Blob Storage
/// en producción, sistema de archivos en local).
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Sube el archivo del pase y devuelve una URL accesible para la clienta
    /// (en Azure Blob: SAS URL con expiración corta).
    /// </summary>
    Task<string> UploadPassAsync(string serialNumber, byte[] passBytes, CancellationToken ct = default);

    /// <summary>Descarga el contenido del pase ya almacenado para un serial. Null si no existe.</summary>
    Task<byte[]?> DownloadPassAsync(string serialNumber, CancellationToken ct = default);
}
