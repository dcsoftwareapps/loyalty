using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Campaigns;

public sealed record PointCampaignAdminDto(
    Guid Id,
    string Name,
    string Description,
    int Multiplier,
    decimal? MinimumPurchaseAmount,
    CampaignLevelEligibility LevelEligibility,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsCurrentlyActive);
