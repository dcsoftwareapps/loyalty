using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Repositories;

/// <summary>Persistence for local links to external wallet provider objects.</summary>
public interface IMemberDigitalWalletRepository
{
    Task<MemberDigitalWallet?> GetByLoyaltyCardAndProviderAsync(
        Guid loyaltyCardId,
        DigitalWalletProvider provider,
        CancellationToken ct = default);

    Task<MemberDigitalWallet?> GetByExternalObjectIdAsync(
        DigitalWalletProvider provider,
        string externalObjectId,
        CancellationToken ct = default);

    Task<Guid?> GetOldestTenantIdByExternalClassIdAsync(
        DigitalWalletProvider provider,
        string externalClassId,
        CancellationToken ct = default);

    Task AddAsync(MemberDigitalWallet wallet, CancellationToken ct = default);

    void Update(MemberDigitalWallet wallet);
}

