using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Application.Common.Interfaces;

/// <summary>
/// Genera el archivo <c>.pkpass</c> firmado con el certificado de Apple.
/// La implementación lee el certificado de Azure Key Vault — nunca del disco.
/// </summary>
public interface IPassGeneratorService
{
    /// <summary>
    /// Construye <c>pass.json</c>, calcula el <c>manifest.json</c> con SHA-1
    /// por archivo, firma con el certificado, y empaqueta como ZIP <c>.pkpass</c>.
    /// </summary>
    Task<byte[]> GeneratePassAsync(LoyaltyCard card, Customer customer, CancellationToken ct = default);

    /// <summary>
    /// URL de descarga del pase actualmente almacenado para este serial.
    /// Útil para reenviar el pase a la clienta sin regenerarlo.
    /// </summary>
    Task<string> GetPassDownloadUrlAsync(string serialNumber, CancellationToken ct = default);
}
