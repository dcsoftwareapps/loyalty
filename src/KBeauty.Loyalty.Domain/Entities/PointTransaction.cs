using KBeauty.Loyalty.Domain.Common;
using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Domain.Entities;

/// <summary>
/// Registro inmutable de un movimiento de puntos en una tarjeta.
/// Una vez creado nunca se modifica — es el "diario contable" del programa.
/// </summary>
public class PointTransaction : Entity
{
    /// <summary>Tarjeta a la que pertenece el movimiento.</summary>
    public Guid LoyaltyCardId { get; private set; }

    /// <summary>
    /// Delta de puntos: positivo para Purchase / BonusXxx, negativo para
    /// Redemption / Expiry. Persiste signed para poder reconstruir el saldo
    /// con SUM(Points).
    /// </summary>
    public int Points { get; private set; }

    /// <summary>Categoría del movimiento.</summary>
    public TransactionType Type { get; private set; }

    /// <summary>Modificador opcional (ej: Birthday cuando la compra recibió x2).</summary>
    public BonusType? BonusType { get; private set; }

    /// <summary>Descripción legible para reportes y para mostrar en el historial de la clienta.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Monto de la compra original (solo para Type=Purchase).</summary>
    public decimal? PurchaseAmount { get; private set; }

    /// <summary>Campaña que definió el multiplicador efectivo de la compra, si aplica.</summary>
    public Guid? CampaignId { get; private set; }

    /// <summary>Puntos base calculados antes de multiplicadores, solo para compras auditables.</summary>
    public int? BasePoints { get; private set; }

    /// <summary>Multiplicador efectivo aplicado a la compra, solo para compras auditables.</summary>
    public decimal? AppliedMultiplier { get; private set; }

    public PointCampaign? Campaign { get; private set; }

    /// <summary>Timestamp UTC del movimiento.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Identificador del operador que registró el movimiento (auditoría).</summary>
    public string? CreatedBy { get; private set; }

    private PointTransaction() { }

    public PointTransaction(
        Guid id,
        Guid loyaltyCardId,
        int points,
        TransactionType type,
        string description,
        DateTime createdAtUtc,
        BonusType? bonusType = null,
        decimal? purchaseAmount = null,
        string? createdBy = null,
        Guid? campaignId = null,
        int? basePoints = null,
        decimal? appliedMultiplier = null) : base(id)
    {
        if (loyaltyCardId == Guid.Empty)
            throw new ArgumentException("LoyaltyCardId requerido.", nameof(loyaltyCardId));
        if (points == 0)
            throw new ArgumentException("Una transacción no puede tener 0 puntos.", nameof(points));

        LoyaltyCardId = loyaltyCardId;
        Points = points;
        Type = type;
        BonusType = bonusType;
        Description = description?.Trim() ?? string.Empty;
        PurchaseAmount = purchaseAmount;
        CreatedAt = createdAtUtc;
        CreatedBy = createdBy?.Trim();
        CampaignId = campaignId;
        BasePoints = basePoints;
        AppliedMultiplier = appliedMultiplier;
    }
}
