using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class PointLotRepository : IPointLotRepository
{
    private readonly AppDbContext _db;

    public PointLotRepository(AppDbContext db) => _db = db;

    public async Task AddLotAsync(PointLot lot, CancellationToken ct = default)
    {
        await _db.PointLots.AddAsync(lot, ct);
    }

    public async Task AddConsumptionAsync(PointLotConsumption consumption, CancellationToken ct = default)
    {
        await _db.PointLotConsumptions.AddAsync(consumption, ct);
    }

    public async Task<IReadOnlyList<PointLot>> GetAvailableLotsAsync(
        Guid loyaltyCardId,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var lots = await _db.PointLots
            .Where(l => l.LoyaltyCardId == loyaltyCardId
                     && l.RemainingAmount > 0
                     && l.ExpiresAt > nowUtc)
            .OrderBy(l => l.ExpiresAt)
            .ThenBy(l => l.EarnedAt)
            .ThenBy(l => l.Id)
            .ToListAsync(ct);

        return lots.AsReadOnly();
    }

    public async Task<IReadOnlyList<PointLotConsumption>> GetActiveConsumptionsByRedemptionIdAsync(
        Guid redemptionId,
        CancellationToken ct = default)
    {
        var consumptions = await _db.PointLotConsumptions
            .Where(c => c.RedemptionId == redemptionId && c.ReversedAt == null)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        return consumptions.AsReadOnly();
    }

    public Task<PointLot?> GetLotByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.PointLots.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<PointLot>> GetExpiredLotsAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var lots = await _db.PointLots
            .Where(l => l.RemainingAmount > 0 && l.ExpiresAt <= nowUtc)
            .OrderBy(l => l.ExpiresAt)
            .ThenBy(l => l.EarnedAt)
            .ThenBy(l => l.Id)
            .ToListAsync(ct);

        return lots.AsReadOnly();
    }

    public void UpdateLot(PointLot lot)
    {
        if (_db.Entry(lot).State == EntityState.Detached)
            _db.PointLots.Update(lot);
    }

    public void UpdateConsumption(PointLotConsumption consumption)
    {
        if (_db.Entry(consumption).State == EntityState.Detached)
            _db.PointLotConsumptions.Update(consumption);
    }
}
