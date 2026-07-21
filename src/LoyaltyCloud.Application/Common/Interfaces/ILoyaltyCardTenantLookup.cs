namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ILoyaltyCardTenantLookup
{
    Task<WalletTenantInfo?> ResolveBySerialNumberAsync(
        string serialNumber,
        CancellationToken cancellationToken = default);
}

