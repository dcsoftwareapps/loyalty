using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Domain.Repositories;

/// <summary>Acceso persistente al agregado <see cref="LoyaltyCard"/>.</summary>
public interface ILoyaltyCardRepository
{
    /// <summary>Tarjeta de una clienta dada (la relación es 1:1).</summary>
    Task<LoyaltyCard?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Tarjeta por su serial — el caso más usado (al escanear QR).</summary>
    Task<LoyaltyCard?> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default);

    /// <summary>Tarjeta por Id.</summary>
    Task<LoyaltyCard?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Devuelve cards Radiance cuyo aniversario ya pasó y no acumularon el mínimo anual.</summary>
    Task<IReadOnlyList<LoyaltyCard>> GetCardsForLevelRequalificationAsync(CancellationToken ct = default);

    Task<IReadOnlyList<LoyaltyCard>> GetActiveAsync(CancellationToken ct = default);

    Task AddAsync(LoyaltyCard card, CancellationToken ct = default);
    void Update(LoyaltyCard card);
}
