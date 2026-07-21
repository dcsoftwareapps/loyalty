using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class DeviceRegistrationRepository : IDeviceRegistrationRepository
{
    private readonly AppDbContext _db;

    public DeviceRegistrationRepository(AppDbContext db) => _db = db;

    public Task<DeviceRegistration?> GetAsync(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        string serialNumber,
        CancellationToken ct = default) =>
        _db.DeviceRegistrations.FirstOrDefaultAsync(d =>
            d.DeviceLibraryIdentifier == deviceLibraryIdentifier
            && d.PassTypeIdentifier == passTypeIdentifier
            && d.SerialNumber == serialNumber, ct);

    public async Task<IReadOnlyList<DeviceRegistration>> GetBySerialNumberAsync(
        string serialNumber,
        CancellationToken ct = default)
    {
        var list = await _db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.SerialNumber == serialNumber)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<string>> GetUpdatableSerialsAsync(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        DateTime? since,
        CancellationToken ct = default)
    {
        // Apple pasa passesUpdatedSince — devolvemos los serials cuya LastActivityAt
        // (en la card) sea más reciente que ese timestamp.
        // Si Apple no pasa nada, devolvemos todos los registrados para ese device.
        var query = _db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.DeviceLibraryIdentifier == deviceLibraryIdentifier
                     && d.PassTypeIdentifier == passTypeIdentifier);

        var serialsForDevice = await query.Select(d => d.SerialNumber).ToListAsync(ct);
        if (serialsForDevice.Count == 0) return Array.Empty<string>();

        if (since is null) return serialsForDevice.AsReadOnly();

        // Filtra por actividad reciente
        var changed = await _db.LoyaltyCards
            .AsNoTracking()
            .Where(c => serialsForDevice.Contains(c.SerialNumber) && c.LastActivityAt > since)
            .Select(c => c.SerialNumber)
            .ToListAsync(ct);

        return changed.AsReadOnly();
    }

    public async Task AddAsync(DeviceRegistration registration, CancellationToken ct = default)
    {
        await _db.DeviceRegistrations.AddAsync(registration, ct);
    }

    public void Update(DeviceRegistration registration)
    {
        if (_db.Entry(registration).State == EntityState.Detached)
            _db.DeviceRegistrations.Update(registration);
    }

    public void Remove(DeviceRegistration registration)
    {
        _db.DeviceRegistrations.Remove(registration);
    }
}
