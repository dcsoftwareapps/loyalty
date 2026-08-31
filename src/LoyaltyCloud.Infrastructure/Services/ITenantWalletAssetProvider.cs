namespace LoyaltyCloud.Infrastructure.Services;

public interface ITenantWalletAssetProvider
{
    Task<IReadOnlyList<WalletPassAsset>> LoadAssetsAsync(
        Guid tenantId,
        string tenantSlug,
        string? walletLogoBlobName,
        string? logoBlobName,
        bool includeStripImage,
        string? stripImageBlobName,
        CancellationToken cancellationToken = default);

    Task<WalletPassAsset> LoadGoogleLogoAsync(
        Guid tenantId,
        string tenantSlug,
        string? walletLogoBlobName,
        string? logoBlobName,
        CancellationToken cancellationToken = default);
}

public sealed record WalletPassAsset(string Name, byte[] Bytes);
