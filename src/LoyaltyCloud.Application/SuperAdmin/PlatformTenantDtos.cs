using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.SuperAdmin;

public sealed record PlatformTenantListItemDto(
    Guid TenantId,
    string Slug,
    string DisplayName,
    bool IsActive,
    string TimeZoneId,
    DateTime CreatedAt,
    TenantSubscriptionStatus? SubscriptionStatus,
    string? PlanCode,
    DateTime? TrialEndsAt,
    DateTime? PaidThroughUtc,
    DateTime? GracePeriodEndsAt,
    bool IsOperational);

public sealed record PlatformTenantDetailDto(
    Guid TenantId,
    string Slug,
    string DisplayName,
    bool IsActive,
    string TimeZoneId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsOperational,
    PlatformTenantSubscriptionDto? Subscription,
    PlatformTenantBrandingDto? Branding);

public sealed record PlatformTenantSubscriptionDto(
    TenantSubscriptionStatus Status,
    string PlanCode,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    DateTime? PaidThroughUtc,
    DateTime? GracePeriodEndsAt,
    DateTime? LastPaymentAt);

public sealed record PlatformTenantBrandingDto(
    string PrimaryColor,
    string SecondaryColor,
    string? LogoUrl,
    string? SupportPhone,
    string? WhatsAppUrl,
    string? InstagramUrl,
    string? TermsUrl);
