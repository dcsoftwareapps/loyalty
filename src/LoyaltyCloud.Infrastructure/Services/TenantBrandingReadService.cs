using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Branding;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantBrandingReadService : ITenantBrandingReadService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantBrandingLogoUrlProvider _logoUrls;
    private readonly ILogger<TenantBrandingReadService> _logger;

    public TenantBrandingReadService(
        IServiceScopeFactory scopeFactory,
        ITenantContext tenantContext,
        ITenantBrandingLogoUrlProvider logoUrls,
        ILogger<TenantBrandingReadService> logger)
    {
        _scopeFactory = scopeFactory;
        _tenantContext = tenantContext;
        _logoUrls = logoUrls;
        _logger = logger;
    }

    public async Task<TenantBrandingInfo> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
            return Generic();

        var tenantId = _tenantContext.RequireTenantId();
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => new
            {
                tenant.Id,
                tenant.Slug,
                tenant.DisplayName,
                LogoUrl = tenant.Branding == null ? null : tenant.Branding.LogoUrl,
                LogoBlobName = tenant.Branding == null ? null : tenant.Branding.LogoBlobName,
                WalletBackgroundColor = tenant.Branding == null ? null : tenant.Branding.WalletBackgroundColor,
                WalletLogoBlobName = tenant.Branding == null ? null : tenant.Branding.WalletLogoBlobName,
                WalletLogoScalePercent = tenant.Branding == null
                    ? TenantBranding.DefaultWalletLogoScalePercent
                    : tenant.Branding.WalletLogoScalePercent,
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

        var primaryColor = TenantBrandingSanitizer.ColorOrDefault(row.PrimaryColor, TenantBrandingSanitizer.DefaultPrimaryColor, row.Id, "PrimaryColor", _logger);
        var walletBackgroundColor = WalletColorContrast.IsHexColor(row.WalletBackgroundColor)
            ? row.WalletBackgroundColor!.Trim().ToUpperInvariant()
            : null;

        return new TenantBrandingInfo(
            row.Id,
            row.Slug,
            string.IsNullOrWhiteSpace(row.DisplayName) ? TenantBrandingSanitizer.DefaultDisplayName : row.DisplayName,
            primaryColor,
            TenantBrandingSanitizer.ColorOrDefault(row.SecondaryColor, TenantBrandingSanitizer.DefaultSecondaryColor, row.Id, "SecondaryColor", _logger),
            _logoUrls.GetDisplayUrl(row.LogoBlobName)
                ?? TenantBrandingSanitizer.UrlOrNull(row.LogoUrl, row.Id, "LogoUrl", _logger, Uri.UriSchemeHttps, Uri.UriSchemeHttp),
            walletBackgroundColor,
            walletBackgroundColor ?? primaryColor ?? WalletColorContrast.DefaultBackgroundHex,
            _logoUrls.GetDisplayUrl(row.WalletLogoBlobName)
                ?? _logoUrls.GetDisplayUrl(row.LogoBlobName)
                ?? TenantBrandingSanitizer.UrlOrNull(row.LogoUrl, row.Id, "LogoUrl", _logger, Uri.UriSchemeHttps, Uri.UriSchemeHttp),
            !string.IsNullOrWhiteSpace(row.WalletLogoBlobName),
            NormalizeWalletLogoScale(row.WalletLogoScalePercent),
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
            WalletBackgroundColor: null,
            ResolvedWalletBackgroundColor: WalletColorContrast.DefaultBackgroundHex,
            WalletLogoUrl: null,
            HasWalletLogo: false,
            WalletLogoScalePercent: TenantBranding.DefaultWalletLogoScalePercent,
            SupportPhone: null,
            WhatsAppUrl: null,
            InstagramUrl: null,
            TermsUrl: null);

    private static int NormalizeWalletLogoScale(int value) =>
        value is >= TenantBranding.MinWalletLogoScalePercent and <= TenantBranding.MaxWalletLogoScalePercent
            ? value
            : TenantBranding.DefaultWalletLogoScalePercent;
}
