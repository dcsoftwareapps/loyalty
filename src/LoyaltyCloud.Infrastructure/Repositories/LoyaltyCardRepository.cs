using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class LoyaltyCardRepository : ILoyaltyCardRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public LoyaltyCardRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<LoyaltyCard?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default) =>
        _db.LoyaltyCards.FirstOrDefaultAsync(c =>
            c.TenantId == _tenantContext.RequireTenantId() && c.CustomerId == customerId, ct);

    public Task<LoyaltyCard?> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default)
    {
        var normalized = serialNumber.Trim().ToUpperInvariant();
        return _db.LoyaltyCards.FirstOrDefaultAsync(c => c.SerialNumber == normalized, ct);
    }

    public Task<LoyaltyCard?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.LoyaltyCards.FirstOrDefaultAsync(c => c.TenantId == _tenantContext.RequireTenantId() && c.Id == id, ct);

    public async Task<IReadOnlyList<LoyaltyCard>> GetCardsForLevelRequalificationAsync(CancellationToken ct = default)
    {
        // Filtra a nivel SQL las Radiance cuyo aniversario ya pasó y no llegaron al mínimo.
        // El umbral lo dejamos como constante por simplicidad — el job de reset también
        // lee ProgramConfig si quiere reglas dinámicas.
        var now = DateTime.UtcNow;
        var min = LoyaltyConstants.Defaults.RadianceRequalificationPoints;

        var cards = await _db.LoyaltyCards
            .AsNoTracking()
            .Where(c => c.TenantId == _tenantContext.RequireTenantId()
                     && c.Level == LoyaltyConstants.Levels.Radiance
                     && c.IsActive
                     && c.LevelAchievedAt < now.AddYears(-1)
                     && c.PointsEarnedThisYear < min)
            .ToListAsync(ct);

        return cards.AsReadOnly();
    }

    public async Task<IReadOnlyList<LoyaltyCard>> GetActiveAsync(CancellationToken ct = default)
    {
        var cards = await _db.LoyaltyCards
            .Where(c => c.TenantId == _tenantContext.RequireTenantId() && c.IsActive)
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
