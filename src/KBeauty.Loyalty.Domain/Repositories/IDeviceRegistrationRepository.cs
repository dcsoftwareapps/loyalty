using KBeauty.Loyalty.Domain.Entities;

namespace KBeauty.Loyalty.Domain.Repositories;

/// <summary>
/// Persistencia de dispositivos que tienen instalado un pase de Wallet.
/// Lo consultan los endpoints Apple del controlador <c>PassesController</c>
/// y los handlers que envían push (AddPoints, ConfirmRedemption).
/// </summary>
public interface IDeviceRegistrationRepository
{
    /// <summary>Registro específico por la tripleta (device, passType, serial).</summary>
    Task<DeviceRegistration?> GetAsync(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        string serialNumber,
        CancellationToken ct = default);

    /// <summary>Todos los registros de un serial — para hacer push a todos sus dispositivos.</summary>
    Task<IReadOnlyList<DeviceRegistration>> GetBySerialNumberAsync(
        string serialNumber,
        CancellationToken ct = default);

    /// <summary>Serials que han tenido updates desde <paramref name="since"/> para este device.</summary>
    Task<IReadOnlyList<string>> GetUpdatableSerialsAsync(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        DateTime? since,
        CancellationToken ct = default);

    Task AddAsync(DeviceRegistration registration, CancellationToken ct = default);
    void Update(DeviceRegistration registration);
    void Remove(DeviceRegistration registration);
}
