namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed record GoogleWalletClassData(
    string Id,
    string ProgramName,
    string IssuerName,
    string? LogoUri,
    string? HeroImageUri,
    string? HexBackgroundColor);

