using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class ProgramConfigRepository : IProgramConfigRepository
{
    private readonly AppDbContext _db;

    public ProgramConfigRepository(AppDbContext db) => _db = db;

    public Task<ProgramConfig?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        // El upsert necesita tracking — no usamos AsNoTracking aquí.
        return _db.ProgramConfigs.FirstOrDefaultAsync(c => c.Key == key, ct);
    }

    public async Task<IReadOnlyList<ProgramConfig>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.ProgramConfigs
            .AsNoTracking()
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task UpsertAsync(string key, string value, string? updatedBy, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existing = await _db.ProgramConfigs.FirstOrDefaultAsync(c => c.Key == key, ct);

        if (existing is null)
        {
            // No debería crear claves nuevas — el handler ya validó contra LoyaltyConstants.
            // Lo permitimos por compatibilidad con seeders y scripts de migración.
            var entity = new ProgramConfig(
                id: Guid.NewGuid(),
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
