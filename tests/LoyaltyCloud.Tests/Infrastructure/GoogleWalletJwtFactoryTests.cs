using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        var factory = new GoogleWalletJwtFactory(options);
        var walletObject = new GoogleWalletObjectData(
            "issuer.member-kb-1",
            "issuer.loyalty",
            "Ana Lopez",
            "KB-1",
            100,
            "Mist",
            "KB-1",
            true,
            new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            "100 pts",
            "Mist \u2728",
            "Glow",
            "900 pts",
            "Presenta este c\u00f3digo en caja");

        var saveUrl = factory.CreateSaveUrl(
            credentials,
            walletObject,
            new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("https://pay.google.com/gp/v/save/", saveUrl);
        var jwt = saveUrl.Split('/').Last();
        Assert.Equal(3, jwt.Split('.').Length);
        Assert.DoesNotContain("PRIVATE KEY", saveUrl);
        Assert.True(saveUrl.Length < 1800);

        using var payload = JsonDocument.Parse(DecodeBase64Url(jwt.Split('.')[1]));
        var walletReference = payload.RootElement.GetProperty("payload").GetProperty("loyaltyObjects")[0];
        Assert.Equal(walletObject.Id, walletReference.GetProperty("id").GetString());
        Assert.Equal(walletObject.ClassId, walletReference.GetProperty("classId").GetString());
        Assert.Equal(2, walletReference.EnumerateObject().Count());
    }

    private static string DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
