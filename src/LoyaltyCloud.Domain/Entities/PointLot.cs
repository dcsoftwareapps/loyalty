using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

/// <summary>
/// Lote de puntos positivos con su propia fecha de expiracion.
/// Los consumos se aplican FIFO contra estos lotes.
/// </summary>
public class PointLot : Entity
{
    public Guid LoyaltyCardId { get; private set; }
    public Guid SourcePointTransactionId { get; private set; }
    public int OriginalAmount { get; private set; }
    public int RemainingAmount { get; private set; }
    public DateTime EarnedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PointLot() { }

    public PointLot(
        Guid id,
        Guid loyaltyCardId,
        Guid sourcePointTransactionId,
        int amount,
        DateTime earnedAtUtc,
        DateTime expiresAtUtc,
        DateTime createdAtUtc) : base(id)
    {
        if (loyaltyCardId == Guid.Empty)
            throw new ArgumentException("LoyaltyCardId requerido.", nameof(loyaltyCardId));
        if (sourcePointTransactionId == Guid.Empty)
            throw new ArgumentException("SourcePointTransactionId requerido.", nameof(sourcePointTransactionId));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "El lote debe tener puntos positivos.");
        if (expiresAtUtc <= earnedAtUtc)
            throw new ArgumentException("ExpiresAt debe ser posterior a EarnedAt.", nameof(expiresAtUtc));

        LoyaltyCardId = loyaltyCardId;
        SourcePointTransactionId = sourcePointTransactionId;
        OriginalAmount = amount;
        RemainingAmount = amount;
        EarnedAt = earnedAtUtc;
        ExpiresAt = expiresAtUtc;
        CreatedAt = createdAtUtc;
    }

    public void Consume(int amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "El consumo debe ser positivo.");
        if (RemainingAmount < amount)
            throw new InvalidOperationException("El lote no tiene puntos suficientes.");

        RemainingAmount -= amount;
    }

    public void Restore(int amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "La restauracion debe ser positiva.");
        if (RemainingAmount + amount > OriginalAmount)
            throw new InvalidOperationException("No se puede restaurar mas del monto original del lote.");

        RemainingAmount += amount;
    }

    public int ExpireRemaining()
    {
        var amount = RemainingAmount;
        RemainingAmount = 0;
        return amount;
    }
}
