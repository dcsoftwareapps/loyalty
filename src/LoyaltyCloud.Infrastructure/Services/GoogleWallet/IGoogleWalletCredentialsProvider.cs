namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public interface IGoogleWalletCredentialsProvider
{
    Task<GoogleWalletCredentials> GetAsync(CancellationToken ct = default);
}

