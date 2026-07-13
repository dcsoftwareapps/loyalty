using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.ValueObjects;

namespace KBeauty.Loyalty.Application.Common.Interfaces;

public interface ILevelCalculationService
{
    MemberLevel CalculateLevel(int rollingEligiblePoints, ProgramConfigSnapshot config);
    bool IsEligibleForLevelProgress(TransactionType type);
    int CompareLevels(string currentLevel, string newLevel, ProgramConfigSnapshot config);
}
