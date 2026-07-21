using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Extensions;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Points.Commands.AddPoints;

/// <inheritdoc cref="AddPointsCommand"/>
public sealed class AddPointsHandler : IRequestHandler<AddPointsCommand, Result<AddPointsResponse>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly ICustomerRepository _customers;
    private readonly IPointTransactionRepository _transactions;
    private readonly IPointLotRepository _pointLots;
    private readonly IProgramConfigRepository _config;
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IApnService _apn;
    private readonly ILevelCalculationService _levels;
    private readonly IPointCampaignSelector _campaignSelector;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AddPointsHandler> _logger;

    public AddPointsHandler(
        ILoyaltyCardRepository cards,
        ICustomerRepository customers,
        IPointTransactionRepository transactions,
        IPointLotRepository pointLots,
        IProgramConfigRepository config,
        IDeviceRegistrationRepository devices,
        IApnService apn,
        ILevelCalculationService levels,
        IPointCampaignSelector campaignSelector,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ILogger<AddPointsHandler> logger)
    {
        _cards = cards;
        _customers = customers;
        _transactions = transactions;
        _pointLots = pointLots;
        _config = config;
        _devices = devices;
        _apn = apn;
        _levels = levels;
        _campaignSelector = campaignSelector;
        _dt = dt;
        _uow = uow;
        _logger = logger;
    }

    public AddPointsHandler(
        ILoyaltyCardRepository cards,
        ICustomerRepository customers,
        IPointTransactionRepository transactions,
        IPointLotRepository pointLots,
        IProgramConfigRepository config,
        IDeviceRegistrationRepository devices,
        IApnService apn,
        ILevelCalculationService levels,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ILogger<AddPointsHandler> logger)
        : this(cards, customers, transactions, pointLots, config, devices, apn, levels, NullPointCampaignSelector.Instance, dt, uow, logger)
    {
    }

    /// <inheritdoc />
    public async Task<Result<AddPointsResponse>> Handle(AddPointsCommand command, CancellationToken ct)
    {
        var card = await _cards.GetBySerialNumberAsync(command.SerialNumber, ct);
        if (card is null)
            return Result.Fail<AddPointsResponse>($"No se encontro tarjeta con serial '{command.SerialNumber}'.");
        if (!card.IsActive)
            return Result.Fail<AddPointsResponse>("La tarjeta esta inactiva.");

        var customer = await _customers.GetByIdAsync(card.CustomerId, ct);
        if (customer is null || !customer.IsActive)
            return Result.Fail<AddPointsResponse>("La clienta no esta activa.");

        var snapshot = ProgramConfigSnapshot.FromEntries(await _config.GetAllAsync(ct));
        var basePoints = snapshot.CalculatePointsForPurchase(command.PurchaseAmount);
        if (basePoints <= 0)
            return Result.Fail<AddPointsResponse>(
                $"El monto ${command.PurchaseAmount:0.00} es muy bajo para generar puntos.");

        var now = _dt.UtcNow;
        var isBirthMonth = customer.DateOfBirth.IsBirthMonth(now);
        var birthdayMultiplier = isBirthMonth ? snapshot.BirthdayMultiplier : 1;
        var campaign = await _campaignSelector.SelectBestAsync(now, command.PurchaseAmount, card.Level, ct);
        var campaignMultiplier = campaign?.Multiplier ?? 1;
        var campaignWins = campaign is not null && campaignMultiplier >= birthdayMultiplier && campaignMultiplier > 1;
        var birthdayWins = isBirthMonth && birthdayMultiplier > campaignMultiplier && birthdayMultiplier > 1;
        var effectiveMultiplier = Math.Max(1, Math.Max(birthdayMultiplier, campaignMultiplier));
        var finalPoints = basePoints * effectiveMultiplier;
        var campaignBonusPoints = campaignWins ? finalPoints - basePoints : 0;
        var bonusType = birthdayWins ? BonusType.Birthday : (BonusType?)null;
        var campaignId = campaignWins ? campaign!.Id : (Guid?)null;
        var campaignName = campaignWins ? campaign!.Name : null;

        card.EarnPoints(finalPoints, TransactionType.Purchase, snapshot, _dt);

        var description = campaignWins
            ? $"Compra ${command.PurchaseAmount:0.00} - Campana {campaignName} ({effectiveMultiplier}x)"
            : birthdayWins
                ? $"Compra ${command.PurchaseAmount:0.00} (x{effectiveMultiplier} cumpleanos)"
                : $"Compra ${command.PurchaseAmount:0.00}";

        var transactionId = Guid.NewGuid();
        var transactionCreatedAt = now;
        await _transactions.AddAsync(new PointTransaction(
            id: transactionId,
            loyaltyCardId: card.Id,
            points: finalPoints,
            type: TransactionType.Purchase,
            description: description,
            createdAtUtc: transactionCreatedAt,
            bonusType: bonusType,
            purchaseAmount: command.PurchaseAmount,
            createdBy: command.OperatorId,
            campaignId: campaignId,
            basePoints: basePoints,
            appliedMultiplier: effectiveMultiplier), ct);

        await _pointLots.AddLotAsync(new PointLot(
            id: Guid.NewGuid(),
            loyaltyCardId: card.Id,
            sourcePointTransactionId: transactionId,
            amount: finalPoints,
            earnedAtUtc: transactionCreatedAt,
            expiresAtUtc: transactionCreatedAt.AddMonths(snapshot.PointsExpireAfterMonths),
            createdAtUtc: transactionCreatedAt), ct);

        var windowStart = transactionCreatedAt.AddMonths(-12);
        var rollingPoints = await _transactions.GetEligibleLevelPointsAsync(card.Id, windowStart, ct);
        if (_levels.IsEligibleForLevelProgress(TransactionType.Purchase))
            rollingPoints += finalPoints;

        var oldLevel = card.Level;
        var calculatedLevel = _levels.CalculateLevel(rollingPoints, snapshot);
        var levelComparison = _levels.CompareLevels(oldLevel, calculatedLevel.Name, snapshot);
        var levelChanged = card.ApplyCalculatedLevel(calculatedLevel, _dt);
        var leveledUp = levelChanged && levelComparison > 0;
        _cards.Update(card);

        await _uow.SaveChangesAsync(ct);

        if (!levelChanged)
            await TryPushWalletUpdateAsync(card.SerialNumber, PassUpdateReason.PointsAdded, ct);

        return Result.Ok(new AddPointsResponse(
            PointsAdded: finalPoints,
            NewTotal: card.CurrentPoints,
            Level: card.Level,
            LeveledUp: leveledUp,
            BirthdayBonusApplied: birthdayWins,
            BasePoints: basePoints,
            CampaignBonusPoints: campaignBonusPoints,
            AppliedMultiplier: effectiveMultiplier,
            CampaignId: campaignId,
            CampaignName: campaignName));
    }

    private async Task TryPushWalletUpdateAsync(string serial, PassUpdateReason reason, CancellationToken ct)
    {
        try
        {
            var devices = await _devices.GetBySerialNumberAsync(serial, ct);
            foreach (var device in devices)
                await _apn.SendPassUpdateAsync(device.PushToken, reason, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo enviando push de Wallet para serial {Serial}", serial);
        }
    }

    private sealed class NullPointCampaignSelector : IPointCampaignSelector
    {
        public static readonly NullPointCampaignSelector Instance = new();

        public Task<SelectedPointCampaign?> SelectBestAsync(
            DateTime nowUtc,
            decimal purchaseAmount,
            string loyaltyLevel,
            CancellationToken ct = default) =>
            Task.FromResult<SelectedPointCampaign?>(null);
    }
}
