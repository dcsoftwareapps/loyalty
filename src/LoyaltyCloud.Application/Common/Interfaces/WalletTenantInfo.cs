using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Common.Interfaces;

public sealed record WalletTenantInfo(
    Guid TenantId,
    string TenantSlug,
    string TenantDisplayName,
    string TimeZoneId,
    bool IsTenantActive,
    TenantSubscriptionStatus? SubscriptionStatus,
    DateTime? GracePeriodEndsAt,
    bool IsOperational);

