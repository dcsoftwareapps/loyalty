using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Repositories;

namespace LoyaltyCloud.Application.Wallets;

internal sealed class MemberWalletDataService : IMemberWalletDataService
{
    private readonly ICustomerRepository _customers;
    private readonly ILoyaltyCardRepository _cards;
    private readonly ITenantContext _tenantContext;

    public MemberWalletDataService(
        ICustomerRepository customers,
        ILoyaltyCardRepository cards,
        ITenantContext tenantContext)
    {
        _customers = customers;
        _cards = cards;
        _tenantContext = tenantContext;
    }

    public async Task<Result<MemberWalletData>> GetBySerialNumberAsync(
        string serialNumber,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return Result.Fail<MemberWalletData>("Serial requerido.");

        var card = await _cards.GetBySerialNumberAsync(serialNumber, ct);
        if (card is null)
            return Result.Fail<MemberWalletData>($"No se encontro tarjeta con serial '{serialNumber}'.");

        if (card.TenantId != _tenantContext.RequireTenantId())
            return Result.Fail<MemberWalletData>("La tarjeta no pertenece al tenant actual.");

        var customer = await _customers.GetByIdAsync(card.CustomerId, ct);
        if (customer is null)
            return Result.Fail<MemberWalletData>("No se encontro la clienta asociada a la tarjeta.");

        return Result.Ok(new MemberWalletData(
            TenantId: card.TenantId,
            CustomerId: customer.Id,
            LoyaltyCardId: card.Id,
            SerialNumber: card.SerialNumber,
            FullName: customer.FullName,
            Email: customer.Email,
            Phone: customer.Phone,
            CurrentPoints: card.CurrentPoints,
            LifetimePoints: card.LifetimePoints,
            Level: card.Level,
            LevelAchievedAt: card.LevelAchievedAt,
            LastActivityAt: card.LastActivityAt,
            IsActive: customer.IsActive && card.IsActive,
            BarcodeValue: card.SerialNumber));
    }
}

