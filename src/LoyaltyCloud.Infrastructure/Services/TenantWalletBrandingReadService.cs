using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantWalletBrandingReadService : ITenantWalletBrandingReadService
{
    private const string DefaultBackgroundColor = "rgb(250,248,244)";
    private const string DefaultForegroundColor = "rgb(28,28,28)";
    private const string DefaultLabelColor = "rgb(132,124,120)";
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

        return new TenantWalletBrandingDto(
            TenantId: tenantId,
            TenantSlug: row.Slug,
            DisplayName: row.DisplayName,
            OrganizationName: row.DisplayName,
            Description: $"Tarjeta de Lealtad {row.DisplayName}",
            BackgroundColor: DefaultBackgroundColor,
            ForegroundColor: TenantBrandingSanitizer.ToRgbColor(
                TenantBrandingSanitizer.ColorOrDefault(row.PrimaryColor, TenantBrandingSanitizer.DefaultPrimaryColor, tenantId, "PrimaryColor", _logger),
                DefaultForegroundColor),
            LabelColor: TenantBrandingSanitizer.ToRgbColor(
                TenantBrandingSanitizer.ColorOrDefault(row.SecondaryColor, TenantBrandingSanitizer.DefaultSecondaryColor, tenantId, "SecondaryColor", _logger),
                DefaultLabelColor),
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
