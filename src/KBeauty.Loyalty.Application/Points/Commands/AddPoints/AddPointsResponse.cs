namespace KBeauty.Loyalty.Application.Points.Commands.AddPoints;

/// <param name="PointsAdded">Cantidad final acreditada (ya con multiplicador si aplicó).</param>
/// <param name="NewTotal">Saldo de la tarjeta tras la suma.</param>
/// <param name="Level">Nivel resultante (cambió si <see cref="LeveledUp"/>).</param>
/// <param name="LeveledUp">True si esta compra cruzó un umbral de nivel.</param>
/// <param name="BirthdayBonusApplied">True si se aplicó el multiplicador de cumpleaños.</param>
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
