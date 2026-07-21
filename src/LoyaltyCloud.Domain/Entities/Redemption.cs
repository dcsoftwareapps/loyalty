using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Exceptions;

namespace LoyaltyCloud.Domain.Entities;

/// <summary>
/// Solicitud de canje — máquina de estados Pending → Confirmed | Cancelled.
/// La transición la dispara el operador en el panel admin tras entregar
/// el beneficio en tienda.
/// </summary>
public class Redemption : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }

    /// <summary>Tarjeta que canjea.</summary>
    public Guid LoyaltyCardId { get; private set; }

    /// <summary>Beneficio del catálogo elegido al momento del canje.</summary>
    public Guid RewardCatalogItemId { get; private set; }

    /// <summary>Puntos descontados (snapshot — si el costo del catálogo cambia luego, este valor no cambia).</summary>
    public int PointsSpent { get; private set; }

    /// <summary>Estado actual del canje.</summary>
    public RedemptionStatus Status { get; private set; }

    /// <summary>Fecha (UTC) en que la clienta inició el canje.</summary>
    public DateTime RedeemedAt { get; private set; }

    /// <summary>Fecha (UTC) en que el operador confirmó o canceló el canje.</summary>
    public DateTime? ConfirmedAt { get; private set; }

    /// <summary>Operador que resolvió el canje (auditoría).</summary>
    public string? ConfirmedBy { get; private set; }

    /// <summary>Notas opcionales (motivo de cancelación, observaciones del operador).</summary>
    public string? Notes { get; private set; }

    private Redemption() { }

    public Redemption(
        Guid id,
        Guid tenantId,
        Guid loyaltyCardId,
        Guid rewardCatalogItemId,
        int pointsSpent,
        DateTime redeemedAtUtc) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido.", nameof(tenantId));
        if (loyaltyCardId == Guid.Empty)
            throw new ArgumentException("LoyaltyCardId requerido.", nameof(loyaltyCardId));
        if (rewardCatalogItemId == Guid.Empty)
            throw new ArgumentException("RewardCatalogItemId requerido.", nameof(rewardCatalogItemId));
        if (pointsSpent <= 0)
            throw new ArgumentOutOfRangeException(nameof(pointsSpent), "Debe descontar al menos 1 punto.");

        TenantId = tenantId;
        LoyaltyCardId = loyaltyCardId;
        RewardCatalogItemId = rewardCatalogItemId;
        PointsSpent = pointsSpent;
        Status = RedemptionStatus.Pending;
        RedeemedAt = redeemedAtUtc;
    }

    /// <summary>
    /// Marca el canje como confirmado (beneficio entregado).
    /// Lanza si ya estaba resuelto.
    /// </summary>
    public void Confirm(string confirmedBy, DateTime nowUtc, string? notes = null)
    {
        if (Status != RedemptionStatus.Pending)
            throw new RedemptionAlreadyConfirmedException(Id);

        Status = RedemptionStatus.Confirmed;
        ConfirmedAt = nowUtc;
        ConfirmedBy = confirmedBy?.Trim();
        Notes = notes?.Trim();
    }

    /// <summary>
    /// Cancela el canje. El handler en Application debe re-acreditar los puntos
    /// a la tarjeta vía <c>LoyaltyCard.EarnPoints</c> (con TransactionType auxiliar).
    /// </summary>
    public void Cancel(string cancelledBy, DateTime nowUtc, string? reason = null)
    {
        if (Status != RedemptionStatus.Pending)
            throw new RedemptionAlreadyConfirmedException(Id);

        Status = RedemptionStatus.Cancelled;
        ConfirmedAt = nowUtc;
        ConfirmedBy = cancelledBy?.Trim();
        Notes = reason?.Trim();
    }
}
