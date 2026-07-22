namespace LoyaltyCloud.Application.Points.Commands.AddPoints;

/// <param name="PointsAdded">Cantidad final acreditada, ya con multiplicadores.</param>
/// <param name="NewTotal">Saldo de la tarjeta tras la suma.</param>
/// <param name="Level">Nivel resultante.</param>
/// <param name="LeveledUp">True si esta compra cruzo un umbral de nivel.</param>
/// <param name="BirthdayBonusApplied">True si se aplico el multiplicador de cumpleanos.</param>
/// <param name="BasePoints">Puntos base de la compra antes de bonos.</param>
/// <param name="CampaignBonusPoints">Puntos adicionales por campana.</param>
/// <param name="AppliedMultiplier">Multiplicador efectivo aplicado.</param>
/// <param name="CampaignId">Identificador de la campana aplicada, si hubo una.</param>
/// <param name="CampaignName">Nombre de la campana aplicada, si hubo una.</param>
public sealed record AddPointsResponse(
    int PointsAdded,
    int NewTotal,
    string Level,
    bool LeveledUp,
    bool BirthdayBonusApplied,
    int BasePoints,
    int CampaignBonusPoints,
    decimal AppliedMultiplier,
    Guid? CampaignId,
    string? CampaignName);
