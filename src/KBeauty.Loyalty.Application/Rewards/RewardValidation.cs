using KBeauty.Loyalty.Common.Constants;

namespace KBeauty.Loyalty.Application.Rewards;

internal static class RewardValidation
{
    public static bool IsValidMemberLevel(string? level) =>
        string.Equals(level, LoyaltyConstants.Levels.Mist, StringComparison.Ordinal) ||
        string.Equals(level, LoyaltyConstants.Levels.Glow, StringComparison.Ordinal) ||
        string.Equals(level, LoyaltyConstants.Levels.Radiance, StringComparison.Ordinal);

    public static bool HasValidDateRange(DateTime? validFrom, DateTime? validTo) =>
        !validFrom.HasValue || !validTo.HasValue || validTo.Value >= validFrom.Value;

    public static bool HasMonthlyProductDates(bool isMonthlyProduct, DateTime? validFrom, DateTime? validTo) =>
        !isMonthlyProduct || (validFrom.HasValue && validTo.HasValue);
}
