namespace LoyaltyCloud.Application.Rewards;

internal static class RewardValidation
{
    public static bool HasValidDateRange(DateTime? validFrom, DateTime? validTo) =>
        !validFrom.HasValue || !validTo.HasValue || validTo.Value >= validFrom.Value;

    public static bool HasMonthlyProductDates(bool isMonthlyProduct, DateTime? validFrom, DateTime? validTo) =>
        !isMonthlyProduct || (validFrom.HasValue && validTo.HasValue);
}
