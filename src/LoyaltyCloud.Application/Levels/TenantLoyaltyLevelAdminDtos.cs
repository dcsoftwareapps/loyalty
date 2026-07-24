namespace LoyaltyCloud.Application.Levels;

public sealed record TenantLoyaltyLevelAdminDto(
    Guid Id,
    string Name,
    int PointsRequired,
    int SortOrder,
    bool IsActive);

public sealed record TenantLoyaltyLevelUpdateItemDto(
    Guid? Id,
    string Name,
    int PointsRequired);

public sealed record UpdateTenantLoyaltyLevelsResultDto(
    IReadOnlyList<TenantLoyaltyLevelAdminDto> Levels,
    int CardsReviewed,
    int CardsChanged,
    int CardsUpgraded,
    int CardsDowngraded,
    int WalletsNotified,
    IReadOnlyList<string> Warnings);
