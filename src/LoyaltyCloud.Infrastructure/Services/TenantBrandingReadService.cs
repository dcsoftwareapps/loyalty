using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantBrandingReadService : ITenantBrandingReadService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TenantBrandingReadService> _logger;

    public TenantBrandingReadService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<TenantBrandingReadService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<TenantBrandingInfo> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
            return Generic();

        var tenantId = _tenantContext.RequireTenantId();
        var row = await _db.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => new
            {
                tenant.Id,
                tenant.Slug,
                tenant.DisplayName,
                LogoUrl = tenant.Branding == null ? null : tenant.Branding.LogoUrl,
                PrimaryColor = tenant.Branding == null ? null : tenant.Branding.PrimaryColor,
                SecondaryColor = tenant.Branding == null ? null : tenant.Branding.SecondaryColor,
                SupportPhone = tenant.Branding == null ? null : tenant.Branding.SupportPhone,
                WhatsAppUrl = tenant.Branding == null ? null : tenant.Branding.WhatsAppUrl,
                InstagramUrl = tenant.Branding == null ? null : tenant.Branding.InstagramUrl,
                TermsUrl = tenant.Branding == null ? null : tenant.Branding.TermsUrl
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return Generic();

        return new TenantBrandingInfo(
            row.Id,
            row.Slug,
            string.IsNullOrWhiteSpace(row.DisplayName) ? TenantBrandingSanitizer.DefaultDisplayName : row.DisplayName,
            TenantBrandingSanitizer.ColorOrDefault(row.PrimaryColor, TenantBrandingSanitizer.DefaultPrimaryColor, row.Id, "PrimaryColor", _logger),
            TenantBrandingSanitizer.ColorOrDefault(row.SecondaryColor, TenantBrandingSanitizer.DefaultSecondaryColor, row.Id, "SecondaryColor", _logger),
            TenantBrandingSanitizer.UrlOrNull(row.LogoUrl, row.Id, "LogoUrl", _logger, Uri.UriSchemeHttps, Uri.UriSchemeHttp),
            TenantBrandingSanitizer.TextOrNull(row.SupportPhone),
            TenantBrandingSanitizer.UrlOrNull(row.WhatsAppUrl, row.Id, "WhatsAppUrl", _logger, Uri.UriSchemeHttps, Uri.UriSchemeHttp, "tel"),
            TenantBrandingSanitizer.UrlOrNull(row.InstagramUrl, row.Id, "InstagramUrl", _logger, Uri.UriSchemeHttps, Uri.UriSchemeHttp),
            TenantBrandingSanitizer.UrlOrNull(row.TermsUrl, row.Id, "TermsUrl", _logger, Uri.UriSchemeHttps, Uri.UriSchemeHttp));
    }

    private static TenantBrandingInfo Generic() =>
        new(
            Guid.Empty,
            string.Empty,
            TenantBrandingSanitizer.DefaultDisplayName,
            TenantBrandingSanitizer.DefaultPrimaryColor,
            TenantBrandingSanitizer.DefaultSecondaryColor,
            LogoUrl: null,
            SupportPhone: null,
            WhatsAppUrl: null,
            InstagramUrl: null,
            TermsUrl: null);
}
