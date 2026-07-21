namespace LoyaltyCloud.Domain.Enums;

/// <summary>
/// Motivo por el que se dispara un push de actualización al pase de Apple Wallet.
/// Apple no recibe el motivo en el payload (siempre va vacío), pero lo usamos
/// internamente para logging y métricas.
/// </summary>
public enum PassUpdateReason
{
    /// <summary>Se sumaron puntos a la tarjeta.</summary>
    PointsAdded = 0,

    /// <summary>La clienta subió de nivel.</summary>
    LevelChanged = 1,

    /// <summary>Se confirmó un canje y el saldo bajó.</summary>
    RedemptionConfirmed = 2,

    /// <summary>Se cancelo un canje y el saldo fue restaurado.</summary>
    RedemptionCancelled = 3,

    /// <summary>Expiraron puntos y el saldo bajo.</summary>
    PointsExpired = 4
}
