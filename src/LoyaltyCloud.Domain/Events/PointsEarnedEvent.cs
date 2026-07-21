namespace LoyaltyCloud.Domain.Events;

/// <summary>
/// Se levanta cada vez que una <c>LoyaltyCard</c> suma puntos.
/// Application lo escucha para disparar el push a Apple Wallet.
/// </summary>
/// <param name="CardId">Id de la tarjeta afectada.</param>
/// <param name="PointsAdded">Puntos sumados en esta operación.</param>
/// <param name="NewTotal">Nuevo saldo actual tras la suma.</param>
/// <param name="LevelChanged">True si esta suma cruzó un umbral de nivel.</param>
/// <param name="NewLevel">Nombre del nivel resultante (mismo que el previo si no cambió).</param>
public sealed record PointsEarnedEvent(
    Guid CardId,
    int PointsAdded,
    int NewTotal,
    bool LevelChanged,
    string NewLevel) : IDomainEvent;
