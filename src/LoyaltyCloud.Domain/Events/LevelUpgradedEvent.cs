namespace LoyaltyCloud.Domain.Events;

/// <summary>
/// Se levanta cuando una clienta sube de nivel (Mist → Glow, Glow → Radiance, etc.).
/// </summary>
/// <param name="CardId">Id de la tarjeta que cambió de nivel.</param>
/// <param name="OldLevel">Nombre del nivel anterior.</param>
/// <param name="NewLevel">Nombre del nuevo nivel alcanzado.</param>
public sealed record LevelUpgradedEvent(
    Guid CardId,
    string OldLevel,
    string NewLevel) : IDomainEvent;
