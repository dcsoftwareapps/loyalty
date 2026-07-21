namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IWalletTenantContextResolver
{
    Task<WalletTenantInfo?> ResolveAndSetTenantAsync(
        string serialNumber,
        bool requireOperational,
        CancellationToken cancellationToken = default);
}

