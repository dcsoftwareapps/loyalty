namespace LoyaltyCloud.Domain.Exceptions;

/// <summary>
/// El canje solicitado exige un nivel superior al de la clienta.
/// Ej: una clienta Mist intenta canjear un beneficio Glow.
/// </summary>
public sealed class LevelNotEligibleException : DomainException
{
    /// <summary>Nivel mínimo requerido por el beneficio.</summary>
    public string RequiredLevel { get; }

    /// <summary>Nivel actual de la clienta al momento del intento.</summary>
    public string CurrentLevel { get; }

    public LevelNotEligibleException(string requiredLevel, string currentLevel)
        : base("LEVEL_NOT_ELIGIBLE",
              $"El beneficio requiere nivel {requiredLevel}; la clienta es {currentLevel}.")
    {
        RequiredLevel = requiredLevel;
        CurrentLevel = currentLevel;
    }
}
