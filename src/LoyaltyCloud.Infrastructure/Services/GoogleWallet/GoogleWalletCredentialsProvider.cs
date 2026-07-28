using System.Text.Json;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

internal sealed class GoogleWalletCredentialsProvider : IGoogleWalletCredentialsProvider
{
    private readonly GoogleWalletOptions _options;

    public GoogleWalletCredentialsProvider(IOptions<GoogleWalletOptions> options)
    {
        _options = options.Value;
    }

    public async Task<GoogleWalletCredentials> GetAsync(CancellationToken ct = default)
    {
        var json = _options.ServiceAccountJson;

        if (string.IsNullOrWhiteSpace(json) && !string.IsNullOrWhiteSpace(_options.ServiceAccountJsonPath))
        {
            if (!File.Exists(_options.ServiceAccountJsonPath))
                throw new InvalidOperationException(
                    $"GoogleWallet:ServiceAccountJsonPath apunta a '{_options.ServiceAccountJsonPath}', pero el archivo no existe.");

            json = await File.ReadAllTextAsync(_options.ServiceAccountJsonPath, ct);
        }

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException(
                "Faltan credenciales Google Wallet. Configure GoogleWallet:ServiceAccountJson o GoogleWallet:ServiceAccountJsonPath mediante user-secrets, variables de entorno o Key Vault.");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var clientEmail = Required(root, "client_email");
        var privateKey = Required(root, "private_key");
        var tokenUri = root.TryGetProperty("token_uri", out var tokenUriElement)
            ? tokenUriElement.GetString()
            : _options.TokenEndpoint;

        if (string.IsNullOrWhiteSpace(tokenUri))
            tokenUri = _options.TokenEndpoint;

        return new GoogleWalletCredentials(clientEmail, privateKey, tokenUri);
    }

    private static string Required(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidOperationException($"El JSON de Service Account no contiene '{property}'.");

        return value.GetString()!;
    }
}

