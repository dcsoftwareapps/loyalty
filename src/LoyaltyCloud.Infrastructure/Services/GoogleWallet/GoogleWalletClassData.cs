namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed record GoogleWalletClassData(
    string Id,
    string ProgramName,
    string IssuerName,
    string? LogoUri,
    string? WideLogoUri,
    string? HeroImageUri,
    string? HexBackgroundColor);

