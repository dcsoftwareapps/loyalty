using LoyaltyCloud.Common.Constants;

namespace LoyaltyCloud.Domain.ValueObjects;

/// <summary>
/// Nivel del programa al que pertenece un cliente en un momento dado.
/// Es un value object; dos niveles con el mismo Name son iguales sin importar la instancia.
/// </summary>
/// <param name="Id">Identificador del nivel configurado por tenant.</param>
/// <param name="Name">Nombre del nivel.</param>
/// <param name="MinPoints">Minimo de puntos inclusivo para pertenecer al nivel.</param>
/// <param name="MaxPoints">Maximo de puntos inclusivo que aun se considera en este nivel.</param>
/// <param name="SortOrder">Orden jerarquico del nivel dentro del tenant.</param>
public sealed record MemberLevel(Guid Id, string Name, int MinPoints, int MaxPoints, int SortOrder)
{
    public MemberLevel(string name, int minPoints, int maxPoints)
        : this(Guid.Empty, name, minPoints, maxPoints, 0)
    {
    }

    /// <summary>
    /// Puntos faltantes para subir al siguiente nivel. Devuelve 0 si ya esta en
    /// el nivel maximo.
    /// </summary>
    public int PointsToNextLevel(int currentPoints) =>
        MaxPoints == int.MaxValue ? 0 : Math.Max(0, (MaxPoints + 1) - currentPoints);

    /// <summary>Indica si este nivel tiene jerarquia mayor o igual al <paramref name="other"/>.</summary>
    public bool IsAtLeast(MemberLevel other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return SortOrder > 0 && other.SortOrder > 0
            ? SortOrder >= other.SortOrder
            : MinPoints >= other.MinPoints;
    }

    /// <summary>
    /// Compatibilidad temporal para Rewards legacy hasta tenantizar su configuracion
    /// de niveles en una fase posterior.
    /// </summary>
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
