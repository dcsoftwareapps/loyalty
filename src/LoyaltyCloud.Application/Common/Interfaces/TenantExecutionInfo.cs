using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Common.Interfaces;

public sealed record TenantExecutionInfo(
    Guid TenantId,
    string Slug,
    string TimeZoneId,
    TenantSubscriptionStatus SubscriptionStatus,
    DateTime? GracePeriodEndsAt,
    bool IsOperational,
    string? SkipReason);

public sealed record TenantExecutionSummary(
    int EligibleTenantCount,
    int SucceededTenantCount,
    int FailedTenantCount,
    int SkippedTenantCount);

