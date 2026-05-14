namespace KBeauty.Loyalty.Domain.Events;

/// <summary>
/// Se levanta cuando una clienta inicia un canje (estado Pending).
/// Útil para notificar al operador en el panel admin que hay un canje por confirmar.
/// </summary>
/// <param name="RedemptionId">Id del canje recién creado.</param>
/// <param name="CardId">Tarjeta que solicitó el canje.</param>
/// <param name="RewardName">Nombre legible del beneficio (snapshot al momento del canje).</param>
/// <param name="PointsSpent">Puntos descontados del saldo.</param>
public sealed record RedemptionRequestedEvent(
    Guid RedemptionId,
    Guid CardId,
    string RewardName,
    int PointsSpent) : IDomainEvent;
