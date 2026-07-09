using System.Globalization;
using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Entities;

namespace KBeauty.Loyalty.Domain.ValueObjects;

/// <summary>
/// Proyección tipada e inmutable de las filas de <see cref="ProgramConfig"/>.
/// Application la construye una sola vez por request usando
/// <see cref="FromEntries"/> y la pasa a la lógica de dominio
/// (LoyaltyCard.EarnPoints, MemberLevel.FromPoints, etc).
/// </summary>
/// <remarks>
/// Pasar este snapshot evita que el dominio haga string-parsing de cada valor en
/// cada operación y centraliza los fallbacks a defaults cuando una clave aún no
/// existe en DB.
/// </remarks>
public sealed record ProgramConfigSnapshot(
    decimal PointsPerPesoUnit,
    int WelcomeBonusPoints,
    int ReferralBonusPoints,
    int BirthdayMultiplier,
    int LevelMistMin,
    int LevelGlowMin,
    int LevelRadianceMin,
    int RadianceRequalificationPoints,
    // TODO Phase 2.1: Replace ProgramConfig reward costs with RewardCatalogItem.PointsCost as the single source of truth.
    int RewardMiniProductPoints,
    int RewardFiftyOffPoints,
    int RewardFocusSkinPoints,
    int RewardMonthlyProductPoints,
    int RewardHundredOffCabinaPoints,
    int RewardFacialOffPoints)
{
    /// <summary>
    /// Construye el snapshot desde la colección de filas de <c>ProgramConfig</c>.
    /// Si alguna clave no está presente, cae al default declarado en
    /// <see cref="LoyaltyConstants.Defaults"/>.
    /// </summary>
    public static ProgramConfigSnapshot FromEntries(IEnumerable<ProgramConfig> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var map = entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);

        return new ProgramConfigSnapshot(
            PointsPerPesoUnit: GetDecimal(map, LoyaltyConstants.ConfigKeys.PointsPerPesoUnit, LoyaltyConstants.Defaults.PointsPerPesoUnit),
            WelcomeBonusPoints: GetInt(map, LoyaltyConstants.ConfigKeys.WelcomeBonusPoints, LoyaltyConstants.Defaults.WelcomeBonusPoints),
            ReferralBonusPoints: GetInt(map, LoyaltyConstants.ConfigKeys.ReferralBonusPoints, LoyaltyConstants.Defaults.ReferralBonusPoints),
            BirthdayMultiplier: GetInt(map, LoyaltyConstants.ConfigKeys.BirthdayMultiplier, LoyaltyConstants.Defaults.BirthdayMultiplier),
            LevelMistMin: GetInt(map, LoyaltyConstants.ConfigKeys.LevelMistMin, LoyaltyConstants.Defaults.LevelMistMin),
            LevelGlowMin: GetInt(map, LoyaltyConstants.ConfigKeys.LevelGlowMin, LoyaltyConstants.Defaults.LevelGlowMin),
            LevelRadianceMin: GetInt(map, LoyaltyConstants.ConfigKeys.LevelRadianceMin, LoyaltyConstants.Defaults.LevelRadianceMin),
            RadianceRequalificationPoints: GetInt(map, LoyaltyConstants.ConfigKeys.RadianceRequalificationPoints, LoyaltyConstants.Defaults.RadianceRequalificationPoints),
            RewardMiniProductPoints: GetInt(map, LoyaltyConstants.ConfigKeys.RewardMiniProductPoints, 300),
            RewardFiftyOffPoints: GetInt(map, LoyaltyConstants.ConfigKeys.RewardFiftyOffPoints, 500),
            RewardFocusSkinPoints: GetInt(map, LoyaltyConstants.ConfigKeys.RewardFocusSkinPoints, 400),
            RewardMonthlyProductPoints: GetInt(map, LoyaltyConstants.ConfigKeys.RewardMonthlyProductPoints, 700),
            RewardHundredOffCabinaPoints: GetInt(map, LoyaltyConstants.ConfigKeys.RewardHundredOffCabinaPoints, 800),
            RewardFacialOffPoints: GetInt(map, LoyaltyConstants.ConfigKeys.RewardFacialOffPoints, 1200));
    }

    /// <summary>
    /// Calcula cuántos puntos genera una compra con el ratio configurado.
    /// Ej: $250 MXN con ratio 10 → 25 pts. Redondeo hacia abajo
    /// (la clienta solo gana puntos enteros).
    /// </summary>
    public int CalculatePointsForPurchase(decimal purchaseAmount)
    {
        if (purchaseAmount <= 0 || PointsPerPesoUnit <= 0) return 0;
        return (int)Math.Floor(purchaseAmount / PointsPerPesoUnit);
    }

    private static int GetInt(IDictionary<string, string> map, string key, int defaultValue) =>
        map.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;

    private static decimal GetDecimal(IDictionary<string, string> map, string key, decimal defaultValue) =>
        map.TryGetValue(key, out var raw) && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
}
