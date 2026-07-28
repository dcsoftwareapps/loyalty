using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class MemberDigitalWalletRepository : IMemberDigitalWalletRepository
{
    private readonly AppDbContext _db;

    public MemberDigitalWalletRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<MemberDigitalWallet?> GetByLoyaltyCardAndProviderAsync(
        Guid loyaltyCardId,
        DigitalWalletProvider provider,
        CancellationToken ct = default) =>
        _db.MemberDigitalWallets
            .FirstOrDefaultAsync(w => w.LoyaltyCardId == loyaltyCardId && w.Provider == provider, ct);

    public Task<MemberDigitalWallet?> GetByExternalObjectIdAsync(
        DigitalWalletProvider provider,
        string externalObjectId,
        CancellationToken ct = default) =>
        _db.MemberDigitalWallets
            .FirstOrDefaultAsync(w => w.Provider == provider && w.ExternalObjectId == externalObjectId, ct);

    public async Task AddAsync(MemberDigitalWallet wallet, CancellationToken ct = default)
    {
        await _db.MemberDigitalWallets.AddAsync(wallet, ct);
    }

    public void Update(MemberDigitalWallet wallet)
    {
        if (_db.Entry(wallet).State == EntityState.Detached)
            _db.MemberDigitalWallets.Update(wallet);
    }
}

