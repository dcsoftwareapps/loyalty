using LoyaltyCloud.Application.Common.Events;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Events;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Redemptions.Commands.RedeemReward;

/// <inheritdoc cref="RedeemRewardCommand"/>
public sealed class RedeemRewardHandler : IRequestHandler<RedeemRewardCommand, Result<RedemptionResponse>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly IRewardCatalogRepository _rewards;
    private readonly IRedemptionRepository _redemptions;
    private readonly IPointTransactionRepository _transactions;
    private readonly IPointLotRepository _pointLots;
    private readonly IProgramConfigRepository _config;
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IApnService _apn;
    private readonly ILevelCalculationService _levels;
    private readonly ITenantContext _tenantContext;
    private readonly IPublisher _publisher;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RedeemRewardHandler> _logger;

    public RedeemRewardHandler(
        ILoyaltyCardRepository cards,
        IRewardCatalogRepository rewards,
        IRedemptionRepository redemptions,
        IPointTransactionRepository transactions,
        IPointLotRepository pointLots,
        IProgramConfigRepository config,
        IDeviceRegistrationRepository devices,
        IApnService apn,
        ILevelCalculationService levels,
        ITenantContext tenantContext,
        IPublisher publisher,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ILogger<RedeemRewardHandler> logger)
    {
        _cards = cards;
        _rewards = rewards;
        _redemptions = redemptions;
        _transactions = transactions;
        _pointLots = pointLots;
        _config = config;
        _devices = devices;
        _apn = apn;
        _levels = levels;
        _tenantContext = tenantContext;
        _publisher = publisher;
        _dt = dt;
        _uow = uow;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<RedemptionResponse>> Handle(RedeemRewardCommand command, CancellationToken ct)
    {
        var card = await _cards.GetBySerialNumberAsync(command.SerialNumber, ct);
        if (card is null)
            return Result.Fail<RedemptionResponse>($"No se encontró tarjeta '{command.SerialNumber}'.");
        if (card.TenantId != _tenantContext.RequireTenantId())
            return Result.Fail<RedemptionResponse>("La tarjeta no pertenece al tenant actual.");
        if (!card.IsActive)
            return Result.Fail<RedemptionResponse>("La tarjeta está inactiva.");

        var reward = await _rewards.GetByIdAsync(command.RewardCatalogItemId, ct);
        if (reward is null)
            return Result.Fail<RedemptionResponse>("Beneficio no encontrado.");

        if (reward.TenantId != card.TenantId)
            return Result.Fail<RedemptionResponse>("El beneficio no pertenece al mismo tenant de la tarjeta.");

        var now = _dt.UtcNow;
        if (!reward.IsAvailableOn(now))
            return Result.Fail<RedemptionResponse>($"El beneficio '{reward.Name}' no está disponible actualmente.");

        // Elegibilidad por nivel
        var snapshot = ProgramConfigSnapshot.FromEntries(await _config.GetAllAsync(ct));
        var rollingPoints = await _transactions.GetEligibleLevelPointsAsync(card.Id, now.AddMonths(-12), ct);
        var customerLevel = _levels.CalculateLevel(rollingPoints, snapshot);
        if (!reward.IsEligibleFor(customerLevel, snapshot))
            return Result.Fail<RedemptionResponse>(
                $"Tu nivel {customerLevel.Name} no alcanza para '{reward.Name}' (requiere {reward.MinLevel}).");

        // Validación de saldo (resultado esperado — no excepción)
        if (card.CurrentPoints < reward.PointsCost)
            return Result.Fail<RedemptionResponse>(
                $"Saldo insuficiente: necesitas {reward.PointsCost} y tienes {card.CurrentPoints}.");

        var lots = await _pointLots.GetAvailableLotsAsync(card.Id, now, ct);
        var availableLotPoints = lots.Sum(l => l.RemainingAmount);
        if (availableLotPoints < reward.PointsCost)
            return Result.Fail<RedemptionResponse>(
                $"Saldo disponible insuficiente: necesitas {reward.PointsCost} puntos no vencidos y tienes {availableLotPoints}.");

        // Mutar dominio
        card.RedeemPoints(reward.PointsCost);
        card.Touch(_dt);
        _cards.Update(card);

        var redemption = new Redemption(
            id: Guid.NewGuid(),
            tenantId: card.TenantId,
            loyaltyCardId: card.Id,
            rewardCatalogItemId: reward.Id,
            pointsSpent: reward.PointsCost,
            redeemedAtUtc: now);
        await _redemptions.AddAsync(redemption, ct);

        // Diario contable
        var transactionId = Guid.NewGuid();
        await _transactions.AddAsync(new PointTransaction(
            id: transactionId,
            tenantId: card.TenantId,
            loyaltyCardId: card.Id,
            points: -reward.PointsCost,
            type: TransactionType.Redemption,
            description: $"Canje: {reward.Name}",
            createdAtUtc: now,
            createdBy: command.OperatorId), ct);

        await ConsumeLotsAsync(lots, reward.PointsCost, transactionId, redemption.Id, now, ct);

        await _uow.SaveChangesAsync(ct);

        // Notificar: domain event + APN
        await _publisher.Publish(
            new DomainEventNotification<RedemptionRequestedEvent>(
                new RedemptionRequestedEvent(redemption.Id, card.Id, reward.Name, reward.PointsCost)),
            ct);

        await TryPushWalletUpdateAsync(card.SerialNumber, ct);

        return Result.Ok(new RedemptionResponse(
            RedemptionId: redemption.Id,
            RewardName: reward.Name,
            PointsSpent: reward.PointsCost,
            RemainingPoints: card.CurrentPoints,
            Status: redemption.Status,
            RedeemedAt: redemption.RedeemedAt));
    }

    private async Task TryPushWalletUpdateAsync(string serial, CancellationToken ct)
    {
        try
        {
            var devices = await _devices.GetBySerialNumberAsync(serial, ct);
            foreach (var device in devices)
                await _apn.SendPassUpdateAsync(device.PushToken, PassUpdateReason.RedemptionConfirmed, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo enviando push de Wallet para serial {Serial}", serial);
        }
    }

    private async Task ConsumeLotsAsync(
        IReadOnlyList<PointLot> lots,
        int pointsToConsume,
        Guid transactionId,
        Guid redemptionId,
        DateTime now,
        CancellationToken ct)
    {
        var remaining = pointsToConsume;
        foreach (var lot in lots)
        {
            if (remaining == 0)
                break;

            var amount = Math.Min(lot.RemainingAmount, remaining);
            lot.Consume(amount);
            _pointLots.UpdateLot(lot);

            await _pointLots.AddConsumptionAsync(new PointLotConsumption(
                id: Guid.NewGuid(),
                tenantId: lot.TenantId,
                pointLotId: lot.Id,
                consumingPointTransactionId: transactionId,
                amount: amount,
                createdAtUtc: now,
                redemptionId: redemptionId), ct);

            remaining -= amount;
        }
    }
}
