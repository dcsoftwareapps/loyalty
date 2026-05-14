using KBeauty.Loyalty.Common.Constants;

namespace KBeauty.Loyalty.Domain.ValueObjects;

/// <summary>
/// Nivel del programa al que pertenece una clienta en un momento dado.
/// Es un value object — dos niveles con el mismo Name son iguales sin
/// importar la instancia.
/// </summary>
/// <param name="Name">Nombre del nivel (ver <see cref="LoyaltyConstants.Levels"/>).</param>
/// <param name="MinPoints">Mínimo de puntos (inclusivo) para pertenecer al nivel.</param>
/// <param name="MaxPoints">Máximo de puntos (inclusivo) que aún se considera en este nivel.</param>
public sealed record MemberLevel(string Name, int MinPoints, int MaxPoints)
{
    /// <summary>
    /// Determina el nivel correspondiente a un saldo de puntos dado, usando los
    /// umbrales del snapshot de configuración.
    /// </summary>
    public static MemberLevel FromPoints(int points, ProgramConfigSnapshot config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (points >= config.LevelRadianceMin)
            return new MemberLevel(LoyaltyConstants.Levels.Radiance, config.LevelRadianceMin, int.MaxValue);

        if (points >= config.LevelGlowMin)
            return new MemberLevel(LoyaltyConstants.Levels.Glow, config.LevelGlowMin, config.LevelRadianceMin - 1);

        return new MemberLevel(LoyaltyConstants.Levels.Mist, config.LevelMistMin, config.LevelGlowMin - 1);
    }

    /// <summary>
    /// Puntos faltantes para subir al siguiente nivel. Devuelve 0 si ya está en
    /// el nivel máximo (Radiance).
    /// </summary>
    public int PointsToNextLevel(int currentPoints) =>
        MaxPoints == int.MaxValue ? 0 : Math.Max(0, (MaxPoints + 1) - currentPoints);

    /// <summary>Indica si este nivel tiene jerarquía mayor o igual al <paramref name="other"/>.</summary>
    public bool IsAtLeast(MemberLevel other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return MinPoints >= other.MinPoints;
    }

    /// <summary>Indica si este nivel tiene jerarquía mayor o igual al nivel nombrado.</summary>
    public bool IsAtLeast(string levelName, ProgramConfigSnapshot config)
    {
        var threshold = levelName switch
        {
            var n when string.Equals(n, LoyaltyConstants.Levels.Mist, StringComparison.OrdinalIgnoreCase) => config.LevelMistMin,
            var n when string.Equals(n, LoyaltyConstants.Levels.Glow, StringComparison.OrdinalIgnoreCase) => config.LevelGlowMin,
            var n when string.Equals(n, LoyaltyConstants.Levels.Radiance, StringComparison.OrdinalIgnoreCase) => config.LevelRadianceMin,
            _ => throw new ArgumentException($"Nivel desconocido: {levelName}", nameof(levelName))
        };
        return MinPoints >= threshold;
    }
}
