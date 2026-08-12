using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Repositories;

namespace LoyaltyCloud.Application.Wallets;

internal sealed class MemberWalletDataService : IMemberWalletDataService
{
    private const string BarcodeAlternateText = "Presenta este c\u00f3digo en caja";

    private readonly ICustomerRepository _customers;
    private readonly ILoyaltyCardRepository _cards;
    private readonly IPointTransactionRepository _transactions;
    private readonly ITenantLoyaltyLevelReadService _tenantLevels;
    private readonly ILevelProgressService _levelProgress;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dt;

    public MemberWalletDataService(
        ICustomerRepository customers,
        ILoyaltyCardRepository cards,
        IPointTransactionRepository transactions,
        ITenantLoyaltyLevelReadService tenantLevels,
        ILevelProgressService levelProgress,
        ITenantContext tenantContext,
        IDateTimeProvider dt)
    {
        _customers = customers;
        _cards = cards;
        _transactions = transactions;
        _tenantLevels = tenantLevels;
        _levelProgress = levelProgress;
        _tenantContext = tenantContext;
        _dt = dt;
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

        var tenantLevels = await _tenantLevels.GetActiveLevelsAsync(ct);
        var rollingPoints = await _transactions.GetEligibleLevelPointsAsync(
            card.Id,
            _dt.UtcNow.AddMonths(-12),
            ct);
        var progress = _levelProgress.Calculate(rollingPoints, tenantLevels);
        var displayName = GetWalletDisplayName(customer.FullName);
        var levelText = $"{progress.CurrentLevel.Name} \u2728";
        var nextLevelText = progress.IsMaxLevel
            ? "M\u00e1ximo \u2728"
            : progress.NextLevel!.Name;
        var remainingPointsText = progress.IsMaxLevel
            ? "\u2014"
            : $"{progress.PointsToNextLevel} pts";

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
            Level: progress.CurrentLevel.Name,
            LevelAchievedAt: card.LevelAchievedAt,
            LastActivityAt: card.LastActivityAt,
            IsActive: customer.IsActive && card.IsActive,
            BarcodeValue: card.SerialNumber,
            DisplayName: displayName,
            PointsText: $"{Math.Max(0, card.CurrentPoints)} pts",
            LevelText: levelText,
            NextLevelText: nextLevelText,
            RemainingPointsText: remainingPointsText,
            BarcodeAlternateText: BarcodeAlternateText));
    }

    private static string GetWalletDisplayName(string? fullName)
    {
        var trimmed = fullName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return "Cliente";

        var firstName = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstName)
            ? "Cliente"
            : firstName;
    }
}

