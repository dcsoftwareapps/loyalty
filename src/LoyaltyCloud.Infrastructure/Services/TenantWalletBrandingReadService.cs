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
    private const string LegacyKBeautyContact = "@kbeauty_mx\n\nkbeautymx.com\n\n+52 646 238 6962";

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
        var usesLegacyContactFallback = string.IsNullOrWhiteSpace(contactValue);
        if (usesLegacyContactFallback)
        {
            contactValue = LegacyKBeautyContact;
            _logger.LogWarning(
                "Tenant {TenantId} does not have wallet contact branding; using bundled KBeauty contact fallback.",
                tenantId);
        }

        if (string.IsNullOrWhiteSpace(row.LogoUrl))
        {
            _logger.LogWarning(
                "Tenant {TenantId} does not have wallet asset branding; using bundled KBeauty Apple Wallet assets.",
                tenantId);
        }

        return new TenantWalletBrandingDto(
            DisplayName: row.DisplayName,
            OrganizationName: row.DisplayName,
            Description: $"Tarjeta de Lealtad {row.DisplayName}",
            BackgroundColor: DefaultBackgroundColor,
            ForegroundColor: ToRgbColor(row.PrimaryColor, DefaultForegroundColor),
            LabelColor: ToRgbColor(row.SecondaryColor, DefaultLabelColor),
            ContactValue: contactValue!,
            CustomerFallbackName: $"Cliente {row.DisplayName}",
            UsesBundledAssetsFallback: string.IsNullOrWhiteSpace(row.LogoUrl),
            UsesLegacyContactFallback: usesLegacyContactFallback);
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

    private static string ToRgbColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var color = value.Trim();
        if (!color.StartsWith('#') || color.Length != 7)
            return fallback;

        return int.TryParse(color.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
            && int.TryParse(color.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            && int.TryParse(color.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)
            ? $"rgb({r},{g},{b})"
            : fallback;
    }
}

