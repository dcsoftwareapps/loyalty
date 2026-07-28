namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed record GoogleWalletCredentials(
    string ClientEmail,
    string PrivateKeyPem,
    string TokenUri);

