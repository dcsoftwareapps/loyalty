using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed class GoogleWalletJwtFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly GoogleWalletOptions _options;
    private readonly GoogleWalletObjectMapper _mapper;

    public GoogleWalletJwtFactory(IOptions<GoogleWalletOptions> options, GoogleWalletObjectMapper mapper)
    {
        _options = options.Value;
        _mapper = mapper;
    }

    public string CreateSaveUrl(
        GoogleWalletCredentials credentials,
        GoogleWalletObjectData walletObject,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(walletObject);

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = credentials.ClientEmail,
            ["aud"] = "google",
            ["typ"] = "savetowallet",
            ["iat"] = ToUnixSeconds(nowUtc),
            ["origins"] = _options.Origins,
            ["payload"] = new Dictionary<string, object?>
            {
                ["loyaltyObjects"] = new[] { _mapper.ToObjectPayload(walletObject) }
            }
        };

        var jwt = SignJwt(payload, credentials.PrivateKeyPem);
        return $"{_options.SaveUrlBase.TrimEnd('/')}/{jwt}";
    }

    public string CreateOAuthAssertion(
        GoogleWalletCredentials credentials,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = credentials.ClientEmail,
            ["scope"] = "https://www.googleapis.com/auth/wallet_object.issuer",
            ["aud"] = credentials.TokenUri,
            ["iat"] = ToUnixSeconds(nowUtc),
            ["exp"] = ToUnixSeconds(nowUtc.AddMinutes(55))
        };

        return SignJwt(payload, credentials.PrivateKeyPem);
    }

    private static string SignJwt(Dictionary<string, object?> payload, string privateKeyPem)
    {
        var header = new Dictionary<string, object?>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT"
        };

        var encodedHeader = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions));
        var encodedPayload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.AsSpan());
        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static long ToUnixSeconds(DateTime value) =>
        new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds();

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

