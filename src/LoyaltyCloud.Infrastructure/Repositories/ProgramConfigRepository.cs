using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class ProgramConfigRepository : IProgramConfigRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ProgramConfigRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<ProgramConfig?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        // El upsert necesita tracking — no usamos AsNoTracking aquí.
        var tenantId = _tenantContext.RequireTenantId();
        return _db.ProgramConfigs.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Key == key, ct);
    }

    public async Task<IReadOnlyList<ProgramConfig>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.ProgramConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == _tenantContext.RequireTenantId())
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task UpsertAsync(string key, string value, string? updatedBy, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var tenantId = _tenantContext.RequireTenantId();
        var existing = await _db.ProgramConfigs.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Key == key, ct);

        if (existing is null)
        {
            // No debería crear claves nuevas — el handler ya validó contra LoyaltyConstants.
            // Lo permitimos por compatibilidad con seeders y scripts de migración.
            var entity = new ProgramConfig(
                id: Guid.NewGuid(),
                tenantId: tenantId,
                key: key,
                value: value,
                updatedAtUtc: now,
                description: null,
                updatedBy: updatedBy);
            await _db.ProgramConfigs.AddAsync(entity, ct);
        }
        else
        {
            existing.Update(value, now, updatedBy);
        }
    }
}
