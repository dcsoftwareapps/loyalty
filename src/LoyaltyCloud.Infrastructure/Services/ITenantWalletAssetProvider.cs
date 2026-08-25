namespace LoyaltyCloud.Infrastructure.Services;

internal interface ITenantWalletAssetProvider
{
    Task<IReadOnlyList<WalletPassAsset>> LoadAssetsAsync(
        Guid tenantId,
        string tenantSlug,
        string? walletLogoBlobName,
        string? logoBlobName,
        CancellationToken cancellationToken = default);
}

internal sealed record WalletPassAsset(string Name, byte[] Bytes);
