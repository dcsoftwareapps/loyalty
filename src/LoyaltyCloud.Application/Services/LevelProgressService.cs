using LoyaltyCloud.Application.Common.Interfaces;

namespace LoyaltyCloud.Application.Services;

internal sealed class LevelProgressService : ILevelProgressService
{
    private readonly ILevelCalculationService _levelCalculation;

    public LevelProgressService(ILevelCalculationService levelCalculation)
    {
        _levelCalculation = levelCalculation;
    }

    public LevelProgressResult Calculate(
        int rollingEligiblePoints,
        IReadOnlyList<TenantLoyaltyLevelDto> levels)
    {
        var currentLevel = _levelCalculation.CalculateLevel(rollingEligiblePoints, levels);
        var orderedLevels = levels
            .OrderBy(level => level.SortOrder)
            .ThenBy(level => level.Threshold)
            .ToList();

        var currentIndex = orderedLevels.FindIndex(level =>
            string.Equals(level.Name, currentLevel.Name, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
            throw new InvalidOperationException(
                $"El nivel calculado '{currentLevel.Name}' no existe en la configuracion activa del tenant.");

        var nextLevel = currentIndex < orderedLevels.Count - 1
            ? orderedLevels[currentIndex + 1]
            : null;
        var pointsToNext = nextLevel is null
            ? 0
            : Math.Max(0, nextLevel.Threshold - Math.Max(0, rollingEligiblePoints));

        return new LevelProgressResult(
            RollingPoints: Math.Max(0, rollingEligiblePoints),
            CurrentLevel: currentLevel,
            NextLevel: nextLevel,
            PointsToNextLevel: pointsToNext,
            IsMaxLevel: nextLevel is null);
    }
}
