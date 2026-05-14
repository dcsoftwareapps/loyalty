using KBeauty.Loyalty.Application.Common.Events;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.Events;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Application.Redemptions.Commands.RedeemReward;

/// <inheritdoc cref="RedeemRewardCommand"/>
public sealed class RedeemRewardHandler : IRequestHandler<RedeemRewardCommand, Result<RedemptionResponse>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly IRewardCatalogRepository _rewards;
    private readonly IRedemptionRepository _redemptions;
    private readonly IPointTransactionRepository _transactions;
    private readonly IProgramConfigRepository _config;
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IApnService _apn;
    private readonly IPublisher _publisher;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RedeemRewardHandler> _logger;

    public RedeemRewardHandler(
        ILoyaltyCardRepository cards,
        IRewardCatalogRepository rewards,
        IRedemptionRepository redemptions,
        IPointTransactionRepository transactions,
        IProgramConfigRepository config,
        IDeviceRegistrationRepository devices,
        IApnService apn,
        IPublisher publisher,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ILogger<RedeemRewardHandler> logger)
    {
        _cards = cards;
        _rewards = rewards;
        _redemptions = redemptions;
        _transactions = transactions;
        _config = config;
        _devices = devices;
        _apn = apn;
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
        if (!card.IsActive)
            return Result.Fail<RedemptionResponse>("La tarjeta está inactiva.");

        var reward = await _rewards.GetByIdAsync(command.RewardCatalogItemId, ct);
        if (reward is null)
            return Result.Fail<RedemptionResponse>("Beneficio no encontrado.");

        var now = _dt.UtcNow;
        if (!reward.IsAvailableOn(now))
            return Result.Fail<RedemptionResponse>($"El beneficio '{reward.Name}' no está disponible actualmente.");

        // Elegibilidad por nivel
        var snapshot = ProgramConfigSnapshot.FromEntries(await _config.GetAllAsync(ct));
        var customerLevel = MemberLevel.FromPoints(card.CurrentPoints, snapshot);
        if (!reward.IsEligibleFor(customerLevel, snapshot))
            return Result.Fail<RedemptionResponse>(
                $"Tu nivel {customerLevel.Name} no alcanza para '{reward.Name}' (requiere {reward.MinLevel}).");

        // Validación de saldo (resultado esperado — no excepción)
        if (card.CurrentPoints < reward.PointsCost)
            return Result.Fail<RedemptionResponse>(
                $"Saldo insuficiente: necesitas {reward.PointsCost} y tienes {card.CurrentPoints}.");

        // Mutar dominio
        card.RedeemPoints(reward.PointsCost);
        _cards.Update(card);

        var redemption = new Redemption(
            id: Guid.NewGuid(),
            loyaltyCardId: card.Id,
            rewardCatalogItemId: reward.Id,
            pointsSpent: reward.PointsCost,
            redeemedAtUtc: now);
        await _redemptions.AddAsync(redemption, ct);

        // Diario contable
        await _transactions.AddAsync(new PointTransaction(
            id: Guid.NewGuid(),
            loyaltyCardId: card.Id,
            points: -reward.PointsCost,
            type: TransactionType.Redemption,
            description: $"Canje: {reward.Name}",
            createdAtUtc: now,
            createdBy: command.OperatorId), ct);

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
}
