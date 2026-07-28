using LoyaltyCloud.Infrastructure.Services.GoogleWallet;

namespace LoyaltyCloud.Tests.Integration.Fakes;

public sealed class FakeGoogleWalletClient : IGoogleWalletClient
{
    public List<GoogleWalletClassData> Classes { get; } = new();
    public List<GoogleWalletObjectData> Objects { get; } = new();

    public Task EnsureLoyaltyClassAsync(GoogleWalletClassData walletClass, CancellationToken ct = default)
    {
        Classes.Add(walletClass);
        return Task.CompletedTask;
    }

    public Task CreateOrUpdateObjectAsync(GoogleWalletObjectData walletObject, CancellationToken ct = default)
    {
        Objects.Add(walletObject);
        return Task.CompletedTask;
    }
}

