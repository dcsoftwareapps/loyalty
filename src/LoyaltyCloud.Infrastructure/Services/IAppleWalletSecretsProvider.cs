namespace LoyaltyCloud.Infrastructure.Services;

internal interface IAppleWalletSecretsProvider
{
    Task<byte[]> GetPassCertificateBytesAsync(CancellationToken cancellationToken);

    Task<string> GetPassCertificatePasswordAsync(CancellationToken cancellationToken);

    Task<byte[]?> GetWwdrCertificateBytesAsync(CancellationToken cancellationToken);

    Task<string> GetApnPrivateKeyPemAsync(CancellationToken cancellationToken);

    Task<string> GetApnKeyIdAsync(CancellationToken cancellationToken);

    Task<string> GetApnTeamIdAsync(CancellationToken cancellationToken);
}
