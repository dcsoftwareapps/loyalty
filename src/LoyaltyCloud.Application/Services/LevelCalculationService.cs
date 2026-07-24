using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.ValueObjects;

namespace LoyaltyCloud.Application.Services;

internal sealed class LevelCalculationService : ILevelCalculationService
{
    public MemberLevel CalculateLevel(
        int rollingEligiblePoints,
        IReadOnlyList<TenantLoyaltyLevelDto> levels)
    {
        var orderedLevels = ValidateAndOrder(levels);
        var safePoints = Math.Max(0, rollingEligiblePoints);
        var selectedIndex = 0;

        for (var i = 0; i < orderedLevels.Count; i++)
        {
            if (safePoints >= orderedLevels[i].Threshold)
                selectedIndex = i;
        }

        var selected = orderedLevels[selectedIndex];
        var maxPoints = selectedIndex == orderedLevels.Count - 1
            ? int.MaxValue
            : orderedLevels[selectedIndex + 1].Threshold - 1;

        return new MemberLevel(
            selected.Id,
            selected.Name,
            selected.Threshold,
            maxPoints,
            selected.SortOrder);
    }

    public bool IsEligibleForLevelProgress(TransactionType type) =>
        LevelProgressTransactionTypes.Contains(type);

    public int CompareLevels(
        string currentLevel,
        string newLevel,
        IReadOnlyList<TenantLoyaltyLevelDto> levels)
    {
        var orderedLevels = ValidateAndOrder(levels);
        var current = Rank(currentLevel, orderedLevels, allowUnknownAsBelowFirst: true);
        var next = Rank(newLevel, orderedLevels, allowUnknownAsBelowFirst: false);
        return next.CompareTo(current);
    }

    private static int Rank(
        string level,
        IReadOnlyList<TenantLoyaltyLevelDto> levels,
        bool allowUnknownAsBelowFirst)
    {
        var match = levels.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, level, StringComparison.OrdinalIgnoreCase));

        if (match is null && allowUnknownAsBelowFirst)
            return levels.Min(candidate => candidate.SortOrder) - 1;

        if (match is null)
            throw new InvalidOperationException(
                $"El nivel '{level}' no existe en la configuracion activa del tenant.");

        return match.SortOrder;
    }

    private static IReadOnlyList<TenantLoyaltyLevelDto> ValidateAndOrder(
        IReadOnlyList<TenantLoyaltyLevelDto> levels)
    {
        if (levels.Count == 0)
            throw new InvalidOperationException(
                "No hay niveles activos configurados para el tenant actual.");

        var ordered = levels
            .OrderBy(level => level.SortOrder)
            .ThenBy(level => level.Threshold)
            .ToList();

        if (ordered.Count is < 3 or > 5)
            throw new InvalidOperationException(
                "La configuracion de niveles del tenant debe tener entre 3 y 5 niveles activos.");

        if (ordered[0].Threshold != 0)
            throw new InvalidOperationException(
                "El primer nivel activo del tenant debe iniciar en 0 puntos.");

        var duplicateNames = ordered
            .GroupBy(level => level.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateNames is not null)
            throw new InvalidOperationException(
                $"La configuracion de niveles contiene el nombre duplicado '{duplicateNames.Key}'.");

        for (var i = 0; i < ordered.Count; i++)
        {
            var level = ordered[i];
            if (string.IsNullOrWhiteSpace(level.Name))
                throw new InvalidOperationException("La configuracion de niveles contiene un nombre vacio.");

            if (i == 0)
                continue;

            var previous = ordered[i - 1];
            if (level.SortOrder <= previous.SortOrder)
                throw new InvalidOperationException(
                    "La configuracion de niveles debe tener SortOrder estrictamente ascendente.");

            if (level.Threshold <= previous.Threshold)
                throw new InvalidOperationException(
                    "La configuracion de niveles debe tener umbrales estrictamente ascendentes.");
        }

        return ordered.AsReadOnly();
    }
}
