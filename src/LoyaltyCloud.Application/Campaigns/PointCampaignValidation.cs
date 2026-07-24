using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Application.Campaigns;

internal static class PointCampaignValidation
{
    public static bool IsValidMultiplier(int multiplier) => multiplier is >= 2 and <= 5;

    public static async Task<bool> IsValidLevelEligibilityAsync(
        string? eligibility,
        ITenantLoyaltyLevelReadService levels,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(eligibility))
            return false;

        var value = eligibility.Trim();
        if (PointCampaign.IsAllLevels(value))
            return true;

        if (value.Length > 30)
            return false;

        var activeLevels = await levels.GetActiveLevelsAsync(ct);
        return activeLevels.Any(level =>
            string.Equals(level.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasValidDateRange(DateTime startsAtUtc, DateTime endsAtUtc) =>
        endsAtUtc >= startsAtUtc;
}
