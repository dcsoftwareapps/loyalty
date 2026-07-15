using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Application.Campaigns;

internal static class PointCampaignValidation
{
    public static bool IsValidMultiplier(int multiplier) => multiplier is >= 2 and <= 5;

    public static bool IsValidLevelEligibility(CampaignLevelEligibility eligibility) =>
        Enum.IsDefined(typeof(CampaignLevelEligibility), eligibility);

    public static bool HasValidDateRange(DateTime startsAtUtc, DateTime endsAtUtc) =>
        endsAtUtc >= startsAtUtc;
}
