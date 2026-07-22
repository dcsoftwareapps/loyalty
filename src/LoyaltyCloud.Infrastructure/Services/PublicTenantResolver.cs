using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class PublicTenantResolver : IPublicTenantResolver
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<PublicTenantResolver> _logger;

    public PublicTenantResolver(
        AppDbContext db,
        IDateTimeProvider clock,
        ILogger<PublicTenantResolver> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PublicTenantInfo?> ResolveBySlugAsync(
        string tenantSlug,
        CancellationToken cancellationToken = default)
    {
        string normalizedSlug;
        try
        {
            normalizedSlug = Tenant.NormalizeSlug(tenantSlug);
        }
        catch (ArgumentException)
        {
            _logger.LogWarning(
                "Public tenant not found. TenantSlug={TenantSlug}",
                tenantSlug);
            return null;
        }

        var row = await _db.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Slug == normalizedSlug)
            .Select(tenant => new
            {
                tenant.Id,
                tenant.Slug,
                tenant.DisplayName,
                tenant.IsActive,
                LogoUrl = tenant.Branding == null ? null : tenant.Branding.LogoUrl,
                PrimaryColor = tenant.Branding == null ? "#1C1C1C" : tenant.Branding.PrimaryColor,
                SecondaryColor = tenant.Branding == null ? "#E8668E" : tenant.Branding.SecondaryColor,
                SupportPhone = tenant.Branding == null ? null : tenant.Branding.SupportPhone,
                WhatsAppUrl = tenant.Branding == null ? null : tenant.Branding.WhatsAppUrl,
                InstagramUrl = tenant.Branding == null ? null : tenant.Branding.InstagramUrl,
                TermsUrl = tenant.Branding == null ? null : tenant.Branding.TermsUrl,
                SubscriptionStatus = tenant.Subscription == null
                    ? null
                    : (LoyaltyCloud.Domain.Enums.TenantSubscriptionStatus?)tenant.Subscription.Status,
                GracePeriodEndsAt = tenant.Subscription == null
                    ? null
                    : tenant.Subscription.GracePeriodEndsAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            _logger.LogWarning(
                "Public tenant not found. TenantSlug={TenantSlug}",
                normalizedSlug);
            return null;
        }

        var isOperational = row.IsActive
            && row.SubscriptionStatus.HasValue
            && TenantSubscription.IsOperational(
                row.SubscriptionStatus.Value,
                row.GracePeriodEndsAt,
                _clock.UtcNow);

        if (!isOperational)
        {
            _logger.LogWarning(
                "Public tenant unavailable. TenantId={TenantId}, TenantSlug={TenantSlug}, SubscriptionStatus={SubscriptionStatus}",
                row.Id,
                row.Slug,
                row.SubscriptionStatus?.ToString() ?? "none");
        }
        else
        {
            _logger.LogInformation(
                "Public tenant resolved. TenantId={TenantId}, TenantSlug={TenantSlug}",
                row.Id,
                row.Slug);
        }

        return new PublicTenantInfo(
            row.Id,
            row.Slug,
            row.DisplayName,
            row.IsActive,
            row.SubscriptionStatus?.ToString(),
            isOperational,
            row.PrimaryColor,
            row.SecondaryColor,
            row.LogoUrl,
            row.SupportPhone,
            row.WhatsAppUrl,
            row.InstagramUrl,
            row.TermsUrl);
    }
}
