namespace KBeauty.Loyalty.Application.Rewards;

public sealed record RewardAdminDto(
    Guid Id,
    string Name,
    string Description,
    int PointsCost,
    string MinLevel,
    bool IsActive,
    bool IsMonthlyProduct,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    bool IsCurrentlyAvailable);
