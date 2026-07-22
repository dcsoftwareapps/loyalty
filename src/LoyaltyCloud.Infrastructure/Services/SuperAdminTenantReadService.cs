using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.SuperAdmin;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class SuperAdminTenantReadService : ISuperAdminTenantReadService
{
    private readonly AppDbContext _db;

    public SuperAdminTenantReadService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PlatformTenantListItemDto>> ListTenantsAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.DisplayName)
            .Select(t => new PlatformTenantListItemDto(
                t.Id,
                t.Slug,
                t.DisplayName,
                t.IsActive,
                t.TimeZoneId,
                t.CreatedAt,
                t.Subscription == null ? null : t.Subscription.Status,
                t.Subscription == null ? null : t.Subscription.PlanCode,
                t.Subscription == null ? null : t.Subscription.CurrentPeriodEnd,
                t.Subscription == null ? null : t.Subscription.GracePeriodEndsAt))
            .ToListAsync(cancellationToken);

    public async Task<PlatformTenantDetailDto?> GetTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new PlatformTenantDetailDto(
                t.Id,
                t.Slug,
                t.DisplayName,
                t.IsActive,
                t.TimeZoneId,
                t.CreatedAt,
                t.UpdatedAt,
                t.Subscription == null
                    ? null
                    : new PlatformTenantSubscriptionDto(
                        t.Subscription.Status,
                        t.Subscription.PlanCode,
                        t.Subscription.CurrentPeriodStart,
                        t.Subscription.CurrentPeriodEnd,
                        t.Subscription.GracePeriodEndsAt,
                        t.Subscription.LastPaymentAt),
                t.Branding == null
                    ? null
                    : new PlatformTenantBrandingDto(
                        t.Branding.PrimaryColor,
                        t.Branding.SecondaryColor,
                        t.Branding.LogoUrl,
                        t.Branding.SupportPhone,
                        t.Branding.WhatsAppUrl,
                        t.Branding.InstagramUrl,
                        t.Branding.TermsUrl)))
            .FirstOrDefaultAsync(cancellationToken);
}
