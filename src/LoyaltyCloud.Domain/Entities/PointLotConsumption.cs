using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

/// <summary>
/// Asignacion de un movimiento negativo contra un lote positivo.
/// Permite revertir cancelaciones sin crear puntos nuevos.
/// </summary>
public class PointLotConsumption : Entity
{
    public Guid PointLotId { get; private set; }
    public Guid ConsumingPointTransactionId { get; private set; }
    public Guid? RedemptionId { get; private set; }
    public int Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReversedAt { get; private set; }

    public bool IsReversed => ReversedAt.HasValue;

    private PointLotConsumption() { }

    public PointLotConsumption(
        Guid id,
        Guid pointLotId,
        Guid consumingPointTransactionId,
        int amount,
        DateTime createdAtUtc,
        Guid? redemptionId = null) : base(id)
    {
        if (pointLotId == Guid.Empty)
            throw new ArgumentException("PointLotId requerido.", nameof(pointLotId));
        if (consumingPointTransactionId == Guid.Empty)
            throw new ArgumentException("ConsumingPointTransactionId requerido.", nameof(consumingPointTransactionId));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "El consumo debe ser positivo.");

        PointLotId = pointLotId;
        ConsumingPointTransactionId = consumingPointTransactionId;
        RedemptionId = redemptionId;
        Amount = amount;
        CreatedAt = createdAtUtc;
    }

    public void MarkReversed(DateTime reversedAtUtc)
    {
        if (ReversedAt.HasValue)
            return;

        ReversedAt = reversedAtUtc;
    }
}
