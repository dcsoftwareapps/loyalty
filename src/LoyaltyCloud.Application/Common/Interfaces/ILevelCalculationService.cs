using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.ValueObjects;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ILevelCalculationService
{
    MemberLevel CalculateLevel(
        int rollingEligiblePoints,
        IReadOnlyList<TenantLoyaltyLevelDto> levels);

    bool IsEligibleForLevelProgress(TransactionType type);

    int CompareLevels(
        string currentLevel,
        string newLevel,
        IReadOnlyList<TenantLoyaltyLevelDto> levels);
}
