namespace LoyaltyCloud.Infrastructure.Services;

internal interface ITenantWalletAssetProvider
{
    Task<IReadOnlyList<WalletPassAsset>> LoadAssetsAsync(
        string tenantSlug,
        CancellationToken cancellationToken = default);
}

internal sealed record WalletPassAsset(string Name, byte[] Bytes);
