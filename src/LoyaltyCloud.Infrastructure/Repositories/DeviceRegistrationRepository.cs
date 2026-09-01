using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class DeviceRegistrationRepository : IDeviceRegistrationRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public DeviceRegistrationRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<DeviceRegistration?> GetAsync(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        string serialNumber,
        CancellationToken ct = default) =>
        _db.DeviceRegistrations.FirstOrDefaultAsync(d =>
            d.TenantId == _tenantContext.RequireTenantId()
            && d.DeviceLibraryIdentifier == deviceLibraryIdentifier
            && d.PassTypeIdentifier == passTypeIdentifier
            && d.SerialNumber == serialNumber, ct);

    public async Task<IReadOnlyList<DeviceRegistration>> GetBySerialNumberAsync(
        string serialNumber,
        CancellationToken ct = default)
    {
        var list = await _db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.TenantId == _tenantContext.RequireTenantId() && d.SerialNumber == serialNumber)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<string>> GetUpdatableSerialsAsync(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        DateTime? since,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();

        // Apple pasa passesUpdatedSince — devolvemos los serials cuya LastActivityAt
        // (en la card) sea más reciente que ese timestamp.
        // Si Apple no pasa nada, devolvemos todos los registrados para ese device.
        var query = _db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId
                     && d.DeviceLibraryIdentifier == deviceLibraryIdentifier
                     && d.PassTypeIdentifier == passTypeIdentifier);

        var serialsForDevice = await query.Select(d => d.SerialNumber).ToListAsync(ct);
        if (serialsForDevice.Count == 0) return Array.Empty<string>();

        var activeCards =
            from card in _db.LoyaltyCards.AsNoTracking()
            join customer in _db.Customers.AsNoTracking()
                on new { card.TenantId, Id = card.CustomerId }
                equals new { customer.TenantId, customer.Id }
            where card.TenantId == tenantId
                && card.IsActive
                && customer.IsActive
                && serialsForDevice.Contains(card.SerialNumber)
            select card;

        if (since is null)
            return await activeCards
                .Select(c => c.SerialNumber)
                .ToListAsync(ct);

        // Filtra por actividad reciente
        var changed = await activeCards
            .Where(c => c.LastActivityAt > since)
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
