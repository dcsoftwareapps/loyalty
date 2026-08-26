using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Branding;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantWalletBrandingReadService : ITenantWalletBrandingReadService
{
    private const string GenericContactFallback = "LoyaltyCloud";

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ApplePassOptions _options;
    private readonly ILogger<TenantWalletBrandingReadService> _logger;

    public TenantWalletBrandingReadService(
        AppDbContext db,
        ITenantContext tenantContext,
        IOptions<ApplePassOptions> options,
        ILogger<TenantWalletBrandingReadService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TenantWalletBrandingDto> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequireTenantId();

        var row = await _db.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => new
            {
                tenant.DisplayName,
                tenant.Slug,
                LogoUrl = tenant.Branding == null ? null : tenant.Branding.LogoUrl,
                LogoBlobName = tenant.Branding == null ? null : tenant.Branding.LogoBlobName,
                WalletBackgroundColor = tenant.Branding == null ? null : tenant.Branding.WalletBackgroundColor,
                WalletLogoBlobName = tenant.Branding == null ? null : tenant.Branding.WalletLogoBlobName,
                PrimaryColor = tenant.Branding == null ? null : tenant.Branding.PrimaryColor,
                SecondaryColor = tenant.Branding == null ? null : tenant.Branding.SecondaryColor,
                SupportPhone = tenant.Branding == null ? null : tenant.Branding.SupportPhone,
                WhatsAppUrl = tenant.Branding == null ? null : tenant.Branding.WhatsAppUrl,
                InstagramUrl = tenant.Branding == null ? null : tenant.Branding.InstagramUrl,
                TermsUrl = tenant.Branding == null ? null : tenant.Branding.TermsUrl
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            throw new InvalidOperationException($"Tenant actual no existe: {tenantId}.");

        var contactValue = BuildContactValue(row.InstagramUrl, row.TermsUrl, row.WhatsAppUrl, row.SupportPhone);
        var usesContactFallback = string.IsNullOrWhiteSpace(contactValue);
        if (usesContactFallback)
        {
            contactValue = GenericContactFallback;
            _logger.LogWarning(
                "Tenant {TenantId} does not have wallet contact branding; using generic LoyaltyCloud contact fallback.",
                tenantId);
        }

        var primaryColor = TenantBrandingSanitizer.ColorOrDefault(
            row.PrimaryColor,
            TenantBrandingSanitizer.DefaultPrimaryColor,
            tenantId,
            "PrimaryColor",
            _logger);
        var backgroundHex = WalletColorContrast.IsHexColor(row.WalletBackgroundColor)
            ? row.WalletBackgroundColor!.Trim().ToUpperInvariant()
            : WalletColorContrast.NormalizeHexOrDefault(primaryColor);
        var textColors = WalletColorContrast.ResolveTextColors(backgroundHex);

        return new TenantWalletBrandingDto(
            TenantId: tenantId,
            TenantSlug: row.Slug,
            DisplayName: row.DisplayName,
            OrganizationName: row.DisplayName,
            Description: $"Tarjeta de Lealtad {row.DisplayName}",
            BackgroundColor: WalletColorContrast.ToAppleRgb(backgroundHex),
            ForegroundColor: WalletColorContrast.ToAppleRgb(textColors.ForegroundHex),
            LabelColor: WalletColorContrast.ToAppleRgb(textColors.LabelHex),
            BackgroundHex: backgroundHex,
            LogoBlobName: row.LogoBlobName,
            WalletLogoBlobName: row.WalletLogoBlobName,
            ContactValue: contactValue!,
            CustomerFallbackName: $"Cliente {row.DisplayName}",
            UsesBundledAssetsFallback: false,
            UsesLegacyContactFallback: false);
    }

    private static string? BuildContactValue(params string?[] values)
    {
        var lines = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return lines.Length == 0
            ? null
            : string.Join("\n\n", lines);
    }

}
