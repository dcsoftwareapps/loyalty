using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.ValueObjects;

namespace KBeauty.Loyalty.Application.Services;

internal sealed class LevelCalculationService : ILevelCalculationService
{
    public MemberLevel CalculateLevel(int rollingEligiblePoints, ProgramConfigSnapshot config) =>
        MemberLevel.FromPoints(Math.Max(0, rollingEligiblePoints), config);

    public bool IsEligibleForLevelProgress(TransactionType type) =>
        LevelProgressTransactionTypes.Contains(type);

    public int CompareLevels(string currentLevel, string newLevel, ProgramConfigSnapshot config)
    {
        var current = Rank(currentLevel, config);
        var next = Rank(newLevel, config);
        return next.CompareTo(current);
    }

    private static int Rank(string level, ProgramConfigSnapshot config)
    {
        if (string.Equals(level, LoyaltyConstants.Levels.Radiance, StringComparison.OrdinalIgnoreCase))
            return config.LevelRadianceMin;
        if (string.Equals(level, LoyaltyConstants.Levels.Glow, StringComparison.OrdinalIgnoreCase))
            return config.LevelGlowMin;

        return config.LevelMistMin;
    }
}
