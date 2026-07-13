using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Repositories;

internal sealed class LoyaltyCardRepository : ILoyaltyCardRepository
{
    private readonly AppDbContext _db;

    public LoyaltyCardRepository(AppDbContext db) => _db = db;

    public Task<LoyaltyCard?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default) =>
        _db.LoyaltyCards.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

    public Task<LoyaltyCard?> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default)
    {
        var normalized = serialNumber.Trim().ToUpperInvariant();
        return _db.LoyaltyCards.FirstOrDefaultAsync(c => c.SerialNumber == normalized, ct);
    }

    public Task<LoyaltyCard?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.LoyaltyCards.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<LoyaltyCard>> GetCardsForLevelRequalificationAsync(CancellationToken ct = default)
    {
        // Filtra a nivel SQL las Radiance cuyo aniversario ya pasó y no llegaron al mínimo.
        // El umbral lo dejamos como constante por simplicidad — el job de reset también
        // lee ProgramConfig si quiere reglas dinámicas.
        var now = DateTime.UtcNow;
        var min = LoyaltyConstants.Defaults.RadianceRequalificationPoints;

        var cards = await _db.LoyaltyCards
            .AsNoTracking()
            .Where(c => c.Level == LoyaltyConstants.Levels.Radiance
                     && c.IsActive
                     && c.LevelAchievedAt < now.AddYears(-1)
                     && c.PointsEarnedThisYear < min)
            .ToListAsync(ct);

        return cards.AsReadOnly();
    }

    public async Task<IReadOnlyList<LoyaltyCard>> GetActiveAsync(CancellationToken ct = default)
    {
        var cards = await _db.LoyaltyCards
            .Where(c => c.IsActive)
            .OrderBy(c => c.SerialNumber)
            .ToListAsync(ct);

        return cards.AsReadOnly();
    }

    public async Task AddAsync(LoyaltyCard card, CancellationToken ct = default)
    {
        await _db.LoyaltyCards.AddAsync(card, ct);
    }

    public void Update(LoyaltyCard card)
    {
        if (_db.Entry(card).State == EntityState.Detached)
            _db.LoyaltyCards.Update(card);
    }
}
