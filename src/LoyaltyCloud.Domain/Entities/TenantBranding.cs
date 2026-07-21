namespace LoyaltyCloud.Domain.Entities;

public sealed class TenantBranding
{
    public Guid TenantId { get; private set; }
    public string? LogoUrl { get; private set; }
    public string PrimaryColor { get; private set; } = "#1C1C1C";
    public string SecondaryColor { get; private set; } = "#E8668E";
    public string? SupportPhone { get; private set; }
    public string? WhatsAppUrl { get; private set; }
    public string? InstagramUrl { get; private set; }
    public string? TermsUrl { get; private set; }

    public Tenant? Tenant { get; private set; }

    private TenantBranding() { }

    public TenantBranding(
        Guid tenantId,
        string? logoUrl = null,
        string? primaryColor = null,
        string? secondaryColor = null,
        string? supportPhone = null,
        string? whatsAppUrl = null,
        string? instagramUrl = null,
        string? termsUrl = null)
    {
        TenantId = tenantId == Guid.Empty
            ? throw new ArgumentException("TenantId requerido.", nameof(tenantId))
            : tenantId;

        LogoUrl = NormalizeOptional(logoUrl, 1000);
        PrimaryColor = NormalizeColor(primaryColor, "#1C1C1C", nameof(primaryColor));
        SecondaryColor = NormalizeColor(secondaryColor, "#E8668E", nameof(secondaryColor));
        SupportPhone = NormalizeOptional(supportPhone, 50);
        WhatsAppUrl = NormalizeOptional(whatsAppUrl, 1000);
        InstagramUrl = NormalizeOptional(instagramUrl, 1000);
        TermsUrl = NormalizeOptional(termsUrl, 1000);
    }

    private static string NormalizeColor(string? value, string fallback, string paramName)
    {
        var color = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (color.Length > 20)
            throw new ArgumentException($"{paramName} no puede exceder 20 caracteres.", paramName);

        return color;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"Valor no puede exceder {maxLength} caracteres.");

        return trimmed;
    }
}
