using System.Security.Cryptography;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;

namespace LoyaltyCloud.Tests.Integration.Fakes;

public sealed class FakeGoogleWalletCredentialsProvider : IGoogleWalletCredentialsProvider
{
    private readonly GoogleWalletCredentials _credentials;

    public FakeGoogleWalletCredentialsProvider()
    {
        using var rsa = RSA.Create(2048);
        _credentials = new GoogleWalletCredentials(
            "wallet-tests@example.iam.gserviceaccount.com",
            rsa.ExportPkcs8PrivateKeyPem(),
            "https://oauth2.googleapis.com/token");
    }

    public Task<GoogleWalletCredentials> GetAsync(CancellationToken ct = default) =>
        Task.FromResult(_credentials);
}

