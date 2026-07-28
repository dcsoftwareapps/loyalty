using System.Security.Cryptography;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using Microsoft.Extensions.Options;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GoogleWalletJwtFactoryTests
{
    [Fact]
    public void CreateSaveUrl_ShouldGenerateSignedJwtUrlWithoutPersistingSecret()
    {
        using var rsa = RSA.Create(2048);
        var credentials = new GoogleWalletCredentials(
            "wallet@example.iam.gserviceaccount.com",
            rsa.ExportPkcs8PrivateKeyPem(),
            "https://oauth2.googleapis.com/token");
        var options = Options.Create(new GoogleWalletOptions
        {
            SaveUrlBase = "https://pay.google.com/gp/v/save",
            Origins = new[] { "https://admin.test.local" }
        });
        var mapper = new GoogleWalletObjectMapper();
        var factory = new GoogleWalletJwtFactory(options, mapper);
        var walletObject = new GoogleWalletObjectData(
            "issuer.member-kb-1",
            "issuer.loyalty",
            "Ana Lopez",
            "KB-1",
            100,
            "Mist",
            "KB-1",
            true,
            new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));

        var saveUrl = factory.CreateSaveUrl(
            credentials,
            walletObject,
            new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("https://pay.google.com/gp/v/save/", saveUrl);
        var jwt = saveUrl.Split('/').Last();
        Assert.Equal(3, jwt.Split('.').Length);
        Assert.DoesNotContain("PRIVATE KEY", saveUrl);
    }
}

