using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

/// <summary>
/// Registro de un dispositivo que tiene instalado el pase de una clienta.
/// Apple llama POST /v1/devices/.../registrations/... cuando un iPhone agrega
/// el pase a Wallet, y este registro nos da el push token que necesitamos
/// para enviar actualizaciones a ese pase específico.
/// </summary>
public class DeviceRegistration : Entity
{
    /// <summary>Identificador único del dispositivo (lo asigna Apple).</summary>
    public string DeviceLibraryIdentifier { get; private set; } = string.Empty;

    /// <summary>Pass Type Identifier (debe coincidir con el del certificado).</summary>
    public string PassTypeIdentifier { get; private set; } = string.Empty;

    /// <summary>Serial de la tarjeta cuyo pase está registrado en este dispositivo.</summary>
    public string SerialNumber { get; private set; } = string.Empty;

    /// <summary>Token necesario para llamar al APN de Apple y forzar actualización del pase.</summary>
    public string PushToken { get; private set; } = string.Empty;

    /// <summary>Fecha (UTC) del primer registro.</summary>
    public DateTime CreatedAt { get; private set; }

    private DeviceRegistration() { }

    public DeviceRegistration(
        Guid id,
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        string serialNumber,
        string pushToken,
        DateTime createdAtUtc) : base(id)
    {
        if (string.IsNullOrWhiteSpace(deviceLibraryIdentifier))
            throw new ArgumentException("DeviceLibraryIdentifier requerido.", nameof(deviceLibraryIdentifier));
        if (string.IsNullOrWhiteSpace(passTypeIdentifier))
            throw new ArgumentException("PassTypeIdentifier requerido.", nameof(passTypeIdentifier));
        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new ArgumentException("SerialNumber requerido.", nameof(serialNumber));
        if (string.IsNullOrWhiteSpace(pushToken))
            throw new ArgumentException("PushToken requerido.", nameof(pushToken));

        DeviceLibraryIdentifier = deviceLibraryIdentifier.Trim();
        PassTypeIdentifier = passTypeIdentifier.Trim();
        SerialNumber = serialNumber.Trim();
        PushToken = pushToken.Trim();
        CreatedAt = createdAtUtc;
    }

    /// <summary>Apple puede rotar el push token; el repo llama esto al re-registrar.</summary>
    public void UpdatePushToken(string newToken)
    {
        if (string.IsNullOrWhiteSpace(newToken))
            throw new ArgumentException("PushToken requerido.", nameof(newToken));
        PushToken = newToken.Trim();
    }
}
