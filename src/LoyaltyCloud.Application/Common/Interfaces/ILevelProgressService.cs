using LoyaltyCloud.Domain.ValueObjects;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ILevelProgressService
{
    LevelProgressResult Calculate(
        int rollingEligiblePoints,
        IReadOnlyList<TenantLoyaltyLevelDto> levels);
}

public sealed record LevelProgressResult(
    int RollingPoints,
    MemberLevel CurrentLevel,
    TenantLoyaltyLevelDto? NextLevel,
    int PointsToNextLevel,
    bool IsMaxLevel)
{
    public int CurrentLevelThreshold => CurrentLevel.MinPoints;
    public int? NextLevelThreshold => NextLevel?.Threshold;
}
