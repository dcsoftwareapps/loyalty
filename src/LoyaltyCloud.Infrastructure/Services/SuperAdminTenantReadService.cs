using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.SuperAdmin;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class SuperAdminTenantReadService : ISuperAdminTenantReadService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public SuperAdminTenantReadService(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PlatformTenantListItemDto>> ListTenantsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var rows = await _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.DisplayName)
            .Select(t => new
            {
                t.Id,
                t.Slug,
                t.DisplayName,
                t.IsActive,
                t.TimeZoneId,
                t.CreatedAt,
                SubscriptionStatus = t.Subscription == null ? null : (LoyaltyCloud.Domain.Enums.TenantSubscriptionStatus?)t.Subscription.Status,
                PlanCode = t.Subscription == null ? null : t.Subscription.PlanCode,
                TrialEndsAt = t.Subscription == null ? null : t.Subscription.CurrentPeriodEnd,
                PaidThroughUtc = t.Subscription == null ? null : t.Subscription.PaidThroughUtc,
                GracePeriodEndsAt = t.Subscription == null ? null : t.Subscription.GracePeriodEndsAt,
                SuspensionReason = t.Subscription == null ? null : t.Subscription.SuspensionReason
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(t => new PlatformTenantListItemDto(
                t.Id,
                t.Slug,
                t.DisplayName,
                t.IsActive,
                t.TimeZoneId,
                t.CreatedAt,
                t.SubscriptionStatus,
                t.PlanCode,
                t.TrialEndsAt,
                t.PaidThroughUtc,
                t.GracePeriodEndsAt,
                t.SuspensionReason?.ToString(),
                t.IsActive && t.SubscriptionStatus.HasValue && TenantSubscription.IsOperational(
                    t.SubscriptionStatus.Value,
                    t.TrialEndsAt,
                    t.PaidThroughUtc,
                    t.GracePeriodEndsAt,
                    now)))
            .ToList();
    }

    public async Task<PlatformTenantDetailDto?> GetTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var row = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new
            {
                t.Id,
                t.Slug,
                t.DisplayName,
                t.IsActive,
                t.TimeZoneId,
                t.CreatedAt,
                t.UpdatedAt,
                SubscriptionStatus = t.Subscription == null ? null : (LoyaltyCloud.Domain.Enums.TenantSubscriptionStatus?)t.Subscription.Status,
                PlanCode = t.Subscription == null ? null : t.Subscription.PlanCode,
                CurrentPeriodStart = t.Subscription == null ? null : t.Subscription.CurrentPeriodStart,
                CurrentPeriodEnd = t.Subscription == null ? null : t.Subscription.CurrentPeriodEnd,
                PaidThroughUtc = t.Subscription == null ? null : t.Subscription.PaidThroughUtc,
                GracePeriodEndsAt = t.Subscription == null ? null : t.Subscription.GracePeriodEndsAt,
                LastPaymentAt = t.Subscription == null ? null : t.Subscription.LastPaymentAt,
                SuspensionReason = t.Subscription == null ? null : t.Subscription.SuspensionReason,
                Branding = t.Branding == null
                    ? null
                    : new PlatformTenantBrandingDto(
                        t.Branding.PrimaryColor,
                        t.Branding.SecondaryColor,
                        t.Branding.LogoUrl,
                        t.Branding.SupportPhone,
                        t.Branding.WhatsAppUrl,
                        t.Branding.InstagramUrl,
                        t.Branding.TermsUrl)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        var isOperational = row.IsActive
            && row.SubscriptionStatus.HasValue
            && TenantSubscription.IsOperational(
                row.SubscriptionStatus.Value,
                row.CurrentPeriodEnd,
                row.PaidThroughUtc,
                row.GracePeriodEndsAt,
                now);
        var subscription = row.SubscriptionStatus.HasValue
            ? new PlatformTenantSubscriptionDto(
                row.SubscriptionStatus.Value,
                row.PlanCode ?? string.Empty,
                row.CurrentPeriodStart,
                row.CurrentPeriodEnd,
                row.PaidThroughUtc,
                row.GracePeriodEndsAt,
                row.LastPaymentAt,
                row.SuspensionReason?.ToString())
            : null;

        return new PlatformTenantDetailDto(
            row.Id,
            row.Slug,
            row.DisplayName,
            row.IsActive,
            row.TimeZoneId,
            row.CreatedAt,
            row.UpdatedAt,
            isOperational,
            subscription,
            row.Branding);
    }
}
