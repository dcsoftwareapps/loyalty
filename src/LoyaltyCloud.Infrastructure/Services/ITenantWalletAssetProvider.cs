namespace LoyaltyCloud.Infrastructure.Services;

internal interface ITenantWalletAssetProvider
{
    Task<IReadOnlyList<WalletPassAsset>> LoadAssetsAsync(
        Guid tenantId,
        string tenantSlug,
        CancellationToken cancellationToken = default);
}

internal sealed record WalletPassAsset(string Name, byte[] Bytes);
