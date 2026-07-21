using Azure.Security.KeyVault.Secrets;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class KeyVaultAppleWalletSecretsProvider : IAppleWalletSecretsProvider
{
    private const string SecretPassCertificate = "kbeauty-pass-certificate";
    private const string SecretPassCertificatePassword = "kbeauty-pass-certificate-password";
    private const string SecretWwdrCertificate = "kbeauty-wwdr-certificate";
    private const string SecretPrivateKey = "kbeauty-apn-private-key";
    private const string SecretKeyId = "kbeauty-apn-key-id";
    private const string SecretTeamId = "kbeauty-apn-team-id";

    private readonly SecretClient _kv;

    public KeyVaultAppleWalletSecretsProvider(SecretClient kv)
    {
        _kv = kv;
    }

    public async Task<byte[]> GetPassCertificateBytesAsync(CancellationToken cancellationToken)
    {
        var certB64 = await GetRequiredSecretAsync(SecretPassCertificate, cancellationToken);
        return Convert.FromBase64String(certB64);
    }

    public Task<string> GetPassCertificatePasswordAsync(CancellationToken cancellationToken) =>
        GetRequiredSecretAsync(SecretPassCertificatePassword, cancellationToken);

    public async Task<byte[]?> GetWwdrCertificateBytesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var certB64 = await GetRequiredSecretAsync(SecretWwdrCertificate, cancellationToken);
            return Convert.FromBase64String(certB64);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public Task<string> GetApnPrivateKeyPemAsync(CancellationToken cancellationToken) =>
        GetRequiredSecretAsync(SecretPrivateKey, cancellationToken);

    public Task<string> GetApnKeyIdAsync(CancellationToken cancellationToken) =>
        GetRequiredSecretAsync(SecretKeyId, cancellationToken);

    public Task<string> GetApnTeamIdAsync(CancellationToken cancellationToken) =>
        GetRequiredSecretAsync(SecretTeamId, cancellationToken);

    private async Task<string> GetRequiredSecretAsync(string name, CancellationToken cancellationToken)
    {
        var value = (await _kv.GetSecretAsync(name, cancellationToken: cancellationToken)).Value.Value;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"El secreto de Key Vault '{name}' esta vacio.");

        return value;
    }
}
