namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public interface IGoogleWalletClient
{
    Task EnsureLoyaltyClassAsync(GoogleWalletClassData walletClass, CancellationToken ct = default);

    Task CreateOrUpdateObjectAsync(GoogleWalletObjectData walletObject, CancellationToken ct = default);

    Task AddMessageAsync(
        string objectId,
        string header,
        string body,
        string messageId,
        CancellationToken ct = default);
}

