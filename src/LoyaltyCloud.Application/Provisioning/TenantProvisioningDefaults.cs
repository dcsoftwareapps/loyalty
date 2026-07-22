using LoyaltyCloud.Common.Constants;

namespace LoyaltyCloud.Application.Provisioning;

public static class TenantProvisioningDefaults
{
    public const string UpdatedBy = "tenant-provisioning";

    public static IReadOnlyList<(string Key, string Value, string Description)> ProgramConfigRows { get; } =
    [
        (LoyaltyConstants.ConfigKeys.PointsPerPesoUnit, LoyaltyConstants.Defaults.PointsPerPesoUnit.ToString("0.##"), "Pesos MXN por 1 punto."),
        (LoyaltyConstants.ConfigKeys.WelcomeBonusPoints, LoyaltyConstants.Defaults.WelcomeBonusPoints.ToString(), "Puntos al registrarse."),
        (LoyaltyConstants.ConfigKeys.ReferralBonusPoints, LoyaltyConstants.Defaults.ReferralBonusPoints.ToString(), "Puntos por referido confirmado."),
        (LoyaltyConstants.ConfigKeys.BirthdayMultiplier, LoyaltyConstants.Defaults.BirthdayMultiplier.ToString(), "Multiplicador en mes de cumpleanos."),
        (LoyaltyConstants.ConfigKeys.PointsExpirationEnabled, LoyaltyConstants.Defaults.PointsExpirationEnabled ? "true" : "false", "Activa la expiracion automatica de puntos."),
        (LoyaltyConstants.ConfigKeys.PointsExpireAfterMonths, LoyaltyConstants.Defaults.PointsExpireAfterMonths.ToString(), "Meses de vigencia de cada lote de puntos."),
        (LoyaltyConstants.ConfigKeys.LevelMistMin, LoyaltyConstants.Defaults.LevelMistMin.ToString(), "Umbral inicio nivel Mist."),
        (LoyaltyConstants.ConfigKeys.LevelGlowMin, LoyaltyConstants.Defaults.LevelGlowMin.ToString(), "Umbral inicio nivel Glow."),
        (LoyaltyConstants.ConfigKeys.LevelRadianceMin, LoyaltyConstants.Defaults.LevelRadianceMin.ToString(), "Umbral inicio nivel Radiance."),
        (LoyaltyConstants.ConfigKeys.RadianceRequalificationPoints, LoyaltyConstants.Defaults.RadianceRequalificationPoints.ToString(), "Puntos anuales para mantener Radiance."),
        (LoyaltyConstants.ConfigKeys.RewardMiniProductPoints, "300", "Costo legacy MVP de mini producto."),
        (LoyaltyConstants.ConfigKeys.RewardFiftyOffPoints, "500", "Costo legacy MVP de descuento."),
        (LoyaltyConstants.ConfigKeys.RewardFocusSkinPoints, "400", "Costo legacy MVP de FocusSkin."),
        (LoyaltyConstants.ConfigKeys.RewardMonthlyProductPoints, "700", "Costo legacy MVP de producto del mes."),
        (LoyaltyConstants.ConfigKeys.RewardHundredOffCabinaPoints, "800", "Costo legacy MVP de cabina."),
        (LoyaltyConstants.ConfigKeys.RewardFacialOffPoints, "1200", "Costo legacy MVP de facial.")
    ];
}
