using System.Text.RegularExpressions;

namespace LoyaltyCloud.Infrastructure.Configuration;

public sealed partial class GoogleWalletOptions
{
    public const string SectionName = "GoogleWallet";

    public bool Enabled { get; init; }
    public string IssuerId { get; init; } = string.Empty;
    public string ClassSuffix { get; init; } = "loyalty";
    public string ObjectIdPrefix { get; init; } = "member";
    public string ProgramName { get; init; } = "KBeauty Loyalty";
    public string IssuerName { get; init; } = "KBeauty MX";
    public string? LogoUri { get; init; }
    public string? HeroImageUri { get; init; }
    public string? HexBackgroundColor { get; init; } = "#FFFFFF";
    public string[] Origins { get; init; } = Array.Empty<string>();
    public string? ServiceAccountJson { get; init; }
    public string? ServiceAccountJsonPath { get; init; }
    public string ApiBaseUrl { get; init; } = "https://walletobjects.googleapis.com/walletobjects/v1";
    public string TokenEndpoint { get; init; } = "https://oauth2.googleapis.com/token";
    public string SaveUrlBase { get; init; } = "https://pay.google.com/gp/v/save";

    public IReadOnlyList<string> ValidateForEnabled()
    {
        var errors = new List<string>();

        if (!Enabled)
            return errors;

        if (string.IsNullOrWhiteSpace(IssuerId))
            errors.Add("GoogleWallet:IssuerId es requerido cuando GoogleWallet:Enabled=true.");
        if (!IssuerIdPattern().IsMatch(IssuerId.Trim()))
            errors.Add("GoogleWallet:IssuerId debe contener solo letras, numeros, guion, guion bajo o punto.");
        if (string.IsNullOrWhiteSpace(ClassSuffix))
            errors.Add("GoogleWallet:ClassSuffix es requerido.");
        if (string.IsNullOrWhiteSpace(ObjectIdPrefix))
            errors.Add("GoogleWallet:ObjectIdPrefix es requerido.");
        if (string.IsNullOrWhiteSpace(ProgramName))
            errors.Add("GoogleWallet:ProgramName es requerido.");
        if (string.IsNullOrWhiteSpace(IssuerName))
            errors.Add("GoogleWallet:IssuerName es requerido.");
        if (string.IsNullOrWhiteSpace(ServiceAccountJson) && string.IsNullOrWhiteSpace(ServiceAccountJsonPath))
            errors.Add("GoogleWallet:ServiceAccountJson o GoogleWallet:ServiceAccountJsonPath es requerido cuando GoogleWallet esta habilitado.");
        if (!string.IsNullOrWhiteSpace(HexBackgroundColor) && !HexColorPattern().IsMatch(HexBackgroundColor.Trim()))
            errors.Add("GoogleWallet:HexBackgroundColor debe usar formato #RRGGBB.");

        return errors;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex IssuerIdPattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorPattern();
}

