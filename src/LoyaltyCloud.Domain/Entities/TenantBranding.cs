namespace LoyaltyCloud.Domain.Entities;

using LoyaltyCloud.Domain.Enums;

public sealed class TenantBranding
{
    public const int DefaultWalletLogoScalePercent = 100;
    public const int MinWalletLogoScalePercent = 60;
    public const int MaxWalletLogoScalePercent = 100;

    public Guid TenantId { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? LogoBlobName { get; private set; }
    public string? WalletBackgroundColor { get; private set; }
    public string? WalletLogoBlobName { get; private set; }
    public int WalletLogoScalePercent { get; private set; } = DefaultWalletLogoScalePercent;
    public AppleWalletPrimaryContentMode AppleWalletPrimaryContentMode { get; private set; } =
        AppleWalletPrimaryContentMode.CustomerName;
    public string? AppleWalletStripImageBlobName { get; private set; }
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
        LogoBlobName = null;
        PrimaryColor = NormalizeColor(primaryColor, "#1C1C1C", nameof(primaryColor));
        SecondaryColor = NormalizeColor(secondaryColor, "#E8668E", nameof(secondaryColor));
        SupportPhone = NormalizeOptional(supportPhone, 50);
        WhatsAppUrl = NormalizeOptional(whatsAppUrl, 1000);
        InstagramUrl = NormalizeOptional(instagramUrl, 1000);
        TermsUrl = NormalizeOptional(termsUrl, 1000);
    }

    public void SetLogo(string? logoUrl, string logoBlobName)
    {
        if (string.IsNullOrWhiteSpace(logoBlobName))
            throw new ArgumentException("LogoBlobName requerido.", nameof(logoBlobName));

        LogoUrl = NormalizeOptional(logoUrl, 1000);
        LogoBlobName = NormalizeOptional(logoBlobName, 500);
    }

    public void ClearLogo()
    {
        LogoUrl = null;
        LogoBlobName = null;
    }

    public void SetWalletBackgroundColor(string? walletBackgroundColor)
    {
        WalletBackgroundColor = NormalizeWalletColor(walletBackgroundColor);
    }

    public void SetWalletLogo(string walletLogoBlobName)
    {
        if (string.IsNullOrWhiteSpace(walletLogoBlobName))
            throw new ArgumentException("WalletLogoBlobName requerido.", nameof(walletLogoBlobName));

        WalletLogoBlobName = NormalizeOptional(walletLogoBlobName, 500);
    }

    public void ClearWalletLogo()
    {
        WalletLogoBlobName = null;
    }

    public void SetWalletLogoScalePercent(int walletLogoScalePercent)
    {
        if (walletLogoScalePercent < MinWalletLogoScalePercent || walletLogoScalePercent > MaxWalletLogoScalePercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(walletLogoScalePercent),
                $"WalletLogoScalePercent debe estar entre {MinWalletLogoScalePercent} y {MaxWalletLogoScalePercent}.");
        }

        WalletLogoScalePercent = walletLogoScalePercent;
    }

    public void SetAppleWalletPrimaryContentMode(AppleWalletPrimaryContentMode mode)
    {
        if (!Enum.IsDefined(typeof(AppleWalletPrimaryContentMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode), "AppleWalletPrimaryContentMode invalido.");

        AppleWalletPrimaryContentMode = mode;
    }

    public void SetAppleWalletStripImage(string stripImageBlobName)
    {
        if (string.IsNullOrWhiteSpace(stripImageBlobName))
            throw new ArgumentException("AppleWalletStripImageBlobName requerido.", nameof(stripImageBlobName));

        AppleWalletStripImageBlobName = NormalizeOptional(stripImageBlobName, 500);
    }

    private static string NormalizeColor(string? value, string fallback, string paramName)
    {
        var color = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (color.Length > 20)
            throw new ArgumentException($"{paramName} no puede exceder 20 caracteres.", paramName);

        return color;
    }

    private static string? NormalizeWalletColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var color = value.Trim();
        if (color.Length != 7 || color[0] != '#')
            throw new ArgumentException("WalletBackgroundColor debe usar formato #RRGGBB.", nameof(value));

        for (var i = 1; i < color.Length; i++)
        {
            var c = color[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                throw new ArgumentException("WalletBackgroundColor debe usar formato #RRGGBB.", nameof(value));
        }

        return color.ToUpperInvariant();
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
