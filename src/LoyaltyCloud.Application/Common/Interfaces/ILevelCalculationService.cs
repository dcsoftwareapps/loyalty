using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.ValueObjects;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ILevelCalculationService
{
    MemberLevel CalculateLevel(int rollingEligiblePoints, ProgramConfigSnapshot config);
    bool IsEligibleForLevelProgress(TransactionType type);
    int CompareLevels(string currentLevel, string newLevel, ProgramConfigSnapshot config);
}
