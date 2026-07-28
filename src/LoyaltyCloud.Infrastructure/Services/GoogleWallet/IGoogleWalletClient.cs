namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public interface IGoogleWalletClient
{
    Task EnsureLoyaltyClassAsync(GoogleWalletClassData walletClass, CancellationToken ct = default);

    Task CreateOrUpdateObjectAsync(GoogleWalletObjectData walletObject, CancellationToken ct = default);
}

