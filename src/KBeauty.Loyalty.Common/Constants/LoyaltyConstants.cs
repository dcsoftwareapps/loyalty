namespace KBeauty.Loyalty.Common.Constants;

/// <summary>
/// Constantes tipadas para todas las claves de configuración, niveles, y valores
/// fijos del programa. NUNCA usar literales string en código de negocio — todo
/// pasa por aquí para evitar magic strings.
/// </summary>
public static class LoyaltyConstants
{
    /// <summary>Claves usadas en la tabla <c>ProgramConfig</c> para reglas configurables.</summary>
    public static class ConfigKeys
    {
        // Reglas de acumulación
        public const string PointsPerPesoUnit = "points_per_peso_unit";
        public const string WelcomeBonusPoints = "welcome_bonus_points";
        public const string ReferralBonusPoints = "referral_bonus_points";
        public const string BirthdayMultiplier = "birthday_multiplier";

        // Umbrales de nivel
        public const string LevelMistMin = "level_mist_min";
        public const string LevelGlowMin = "level_glow_min";
        public const string LevelRadianceMin = "level_radiance_min";
        public const string RadianceRequalificationPoints = "radiance_requalification_points";

        // Costos de canjes
        public const string RewardMiniProductPoints = "reward_mini_product_points";
        public const string RewardFiftyOffPoints = "reward_fifty_off_points";
        public const string RewardFocusSkinPoints = "reward_focusskin_points";
        public const string RewardMonthlyProductPoints = "reward_monthly_product_points";
        public const string RewardHundredOffCabinaPoints = "reward_hundred_off_cabina_points";
        public const string RewardFacialOffPoints = "reward_facial_off_points";
    }

    /// <summary>Nombres canónicos de los niveles del programa.</summary>
    public static class Levels
    {
        public const string Mist = "Mist";
        public const string Glow = "Glow";
        public const string Radiance = "Radiance";
    }

    /// <summary>Identificadores y valores fijos para integración con Apple Wallet.</summary>
    public static class ApplePass
    {
        /// <summary>Identificador del pase (registrado en Apple Developer).</summary>
        public const string PassTypeIdentifier = "pass.com.kbeautymx.loyalty";

        /// <summary>Content-Type devuelto por el endpoint de descarga del pase.</summary>
        public const string ContentType = "application/vnd.apple.pkpass";

        /// <summary>Esquema del header <c>Authorization</c> que envía Apple.</summary>
        public const string AuthScheme = "ApplePass";

        /// <summary>Endpoint productivo de Apple Push Notifications para passes.</summary>
        public const string ApnHost = "https://api.push.apple.com";
    }

    /// <summary>Roles del operador en la tienda (placeholder para autorización futura).</summary>
    public static class Roles
    {
        public const string Owner = "Owner";
        public const string Operator = "Operator";
    }

    /// <summary>Valores por defecto cuando una clave aún no está poblada en DB.</summary>
    public static class Defaults
    {
        public const decimal PointsPerPesoUnit = 10m;       // 1 pt por cada $10 MXN
        public const int WelcomeBonusPoints = 50;
        public const int ReferralBonusPoints = 150;
        public const int BirthdayMultiplier = 2;
        public const int LevelMistMin = 0;
        public const int LevelGlowMin = 1000;
        public const int LevelRadianceMin = 3000;
        public const int RadianceRequalificationPoints = 500;
    }
}
