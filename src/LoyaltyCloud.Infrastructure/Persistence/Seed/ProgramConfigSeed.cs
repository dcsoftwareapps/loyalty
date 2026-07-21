using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Persistence.Seed;

/// <summary>
/// Pre-carga todas las claves de configuración del programa con sus valores
/// default. Los Ids son fijos para que las migraciones sean idempotentes y
/// no se re-inserten en cada migración nueva.
/// </summary>
internal static class ProgramConfigSeed
{
    private static readonly DateTime SeedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string SeedUser = "system";

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProgramConfig>().HasData(
            New("a1000000-0000-0000-0000-000000000001", LoyaltyConstants.ConfigKeys.PointsPerPesoUnit, "10",
                "Pesos MXN por 1 punto (1 pt cada $10)."),
            New("a1000000-0000-0000-0000-000000000002", LoyaltyConstants.ConfigKeys.WelcomeBonusPoints, "50",
                "Puntos al registrarse."),
            New("a1000000-0000-0000-0000-000000000003", LoyaltyConstants.ConfigKeys.ReferralBonusPoints, "150",
                "Puntos por referido confirmado."),
            New("a1000000-0000-0000-0000-000000000004", LoyaltyConstants.ConfigKeys.BirthdayMultiplier, "2",
                "Multiplicador en mes de cumpleaños."),
            New("a1000000-0000-0000-0000-00000000000f", LoyaltyConstants.ConfigKeys.PointsExpirationEnabled, "true",
                "Activa la expiracion automatica de puntos."),
            New("a1000000-0000-0000-0000-000000000010", LoyaltyConstants.ConfigKeys.PointsExpireAfterMonths, "12",
                "Meses de vigencia de cada lote de puntos."),
            New("a1000000-0000-0000-0000-000000000005", LoyaltyConstants.ConfigKeys.LevelMistMin, "0",
                "Umbral inicio nivel Mist."),
            New("a1000000-0000-0000-0000-000000000006", LoyaltyConstants.ConfigKeys.LevelGlowMin, "1000",
                "Umbral inicio nivel Glow."),
            New("a1000000-0000-0000-0000-000000000007", LoyaltyConstants.ConfigKeys.LevelRadianceMin, "3000",
                "Umbral inicio nivel Radiance."),
            New("a1000000-0000-0000-0000-000000000008", LoyaltyConstants.ConfigKeys.RadianceRequalificationPoints, "500",
                "Puntos anuales para mantener Radiance."),
            // TODO Phase 2.1: Keep reward costs only in RewardCatalogItem.PointsCost; these config keys are legacy MVP configuration.
            New("a1000000-0000-0000-0000-000000000009", LoyaltyConstants.ConfigKeys.RewardMiniProductPoints, "300",
                "Costo del mini producto."),
            New("a1000000-0000-0000-0000-00000000000a", LoyaltyConstants.ConfigKeys.RewardFiftyOffPoints, "500",
                "Costo del $50 off en compra."),
            New("a1000000-0000-0000-0000-00000000000b", LoyaltyConstants.ConfigKeys.RewardFocusSkinPoints, "400",
                "Costo del análisis FocusSkin (Glow+)."),
            New("a1000000-0000-0000-0000-00000000000c", LoyaltyConstants.ConfigKeys.RewardMonthlyProductPoints, "700",
                "Costo del producto del mes (Glow+)."),
            New("a1000000-0000-0000-0000-00000000000d", LoyaltyConstants.ConfigKeys.RewardHundredOffCabinaPoints, "800",
                "Costo del $100 off en cabina (Glow+)."),
            New("a1000000-0000-0000-0000-00000000000e", LoyaltyConstants.ConfigKeys.RewardFacialOffPoints, "1200",
                "Costo del $300 off en facial (Radiance).")
        );
    }

    // Crea un anónimo con la forma exacta del shadow de EF para HasData
    // (no usamos `new ProgramConfig(...)` porque HasData necesita formato seedable).
    private static object New(string id, string key, string value, string description) => new
    {
        Id = Guid.Parse(id),
        Key = key,
        Value = value,
        Description = description,
        UpdatedAt = SeedDate,
        UpdatedBy = SeedUser
    };
}
