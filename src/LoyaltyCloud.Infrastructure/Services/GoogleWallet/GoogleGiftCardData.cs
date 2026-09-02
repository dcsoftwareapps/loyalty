namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed record GoogleGiftCardClassData(string Id, string IssuerName);

public sealed record GoogleGiftCardObjectData(
    string Id,
    string ClassId,
    string DisplayName,
    string RecipientName,
    string Code,
    decimal Balance,
    string Currency,
    string Status,
    string HexBackgroundColor,
    string? LogoUri,
    string? HeroImageUri,
    DateTime? ExpiresAtUtc);
