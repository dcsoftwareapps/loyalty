using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.ValueObjects;

namespace LoyaltyCloud.Application.Rewards;

internal static class RewardLevelRules
{
    public static string NormalizeMinimumLevel(string? minimumLevel) =>
        string.IsNullOrWhiteSpace(minimumLevel) ? string.Empty : minimumLevel.Trim();

    public static bool HasMinimumLevel(string? minimumLevel) =>
        !string.IsNullOrWhiteSpace(NormalizeMinimumLevel(minimumLevel));

    public static string DisplayMinimumLevel(string? minimumLevel) =>
        HasMinimumLevel(minimumLevel) ? NormalizeMinimumLevel(minimumLevel) : "Todos los niveles";

    public static string? TryCanonicalizeMinimumLevel(
        string? minimumLevel,
        IReadOnlyList<TenantLoyaltyLevelDto> tenantLevels,
        out string canonicalLevel)
    {
        var normalized = NormalizeMinimumLevel(minimumLevel);
        canonicalLevel = normalized;
        if (normalized.Length == 0)
            return null;

        var match = tenantLevels.FirstOrDefault(level =>
            string.Equals(level.Name, normalized, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return $"El nivel minimo '{normalized}' no existe o no esta activo para el tenant actual.";

        canonicalLevel = match.Name;
        return null;
    }

    public static bool IsEligible(
        MemberLevel customerLevel,
        string? rewardMinimumLevel,
        IReadOnlyList<TenantLoyaltyLevelDto> tenantLevels,
        ILevelCalculationService levelCalculation)
    {
        if (!HasMinimumLevel(rewardMinimumLevel))
            return true;

        var normalized = NormalizeMinimumLevel(rewardMinimumLevel);
        if (tenantLevels.All(level => !string.Equals(level.Name, normalized, StringComparison.OrdinalIgnoreCase)))
            return false;

        var minimumLevel = ResolveRequiredLevel(normalized, tenantLevels);
        return levelCalculation.CompareLevels(minimumLevel.Name, customerLevel.Name, tenantLevels) >= 0;
    }

    public static MemberLevel ResolveRequiredLevel(
        string minimumLevel,
        IReadOnlyList<TenantLoyaltyLevelDto> tenantLevels)
    {
        var normalized = NormalizeMinimumLevel(minimumLevel);
        var orderedLevels = tenantLevels
            .OrderBy(level => level.SortOrder)
            .ThenBy(level => level.Threshold)
            .ToList();
        var index = orderedLevels.FindIndex(level =>
            string.Equals(level.Name, normalized, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            throw new InvalidOperationException(
                $"El nivel minimo '{normalized}' no existe o no esta activo para el tenant actual.");

        var selected = orderedLevels[index];
        var maxPoints = index == orderedLevels.Count - 1
            ? int.MaxValue
            : orderedLevels[index + 1].Threshold - 1;

        return new MemberLevel(selected.Id, selected.Name, selected.Threshold, maxPoints, selected.SortOrder);
    }
}
