using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Exceptions;
using LoyaltyCloud.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Redemptions.Commands.CancelRedemption;

public sealed class CancelRedemptionHandler
    : IRequestHandler<CancelRedemptionCommand, Result<CancelRedemptionResponse>>
{
    private readonly IRedemptionRepository _redemptions;
    private readonly ILoyaltyCardRepository _cards;
    private readonly IRewardCatalogRepository _rewards;
    private readonly IPointTransactionRepository _transactions;
    private readonly IPointLotRepository _pointLots;
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IApnService _apn;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CancelRedemptionHandler> _logger;

    public CancelRedemptionHandler(
        IRedemptionRepository redemptions,
        ILoyaltyCardRepository cards,
        IRewardCatalogRepository rewards,
        IPointTransactionRepository transactions,
        IPointLotRepository pointLots,
        IDeviceRegistrationRepository devices,
        IApnService apn,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ILogger<CancelRedemptionHandler> logger)
    {
        _redemptions = redemptions;
        _cards = cards;
        _rewards = rewards;
        _transactions = transactions;
        _pointLots = pointLots;
        _devices = devices;
        _apn = apn;
        _dt = dt;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<CancelRedemptionResponse>> Handle(CancelRedemptionCommand command, CancellationToken ct)
    {
        var redemption = await _redemptions.GetByIdAsync(command.RedemptionId, ct);
        if (redemption is null)
            return Result.Fail<CancelRedemptionResponse>($"No se encontro el canje {command.RedemptionId}.");

        if (redemption.Status != RedemptionStatus.Pending)
            return Result.Fail<CancelRedemptionResponse>($"El canje ya esta en estado {redemption.Status}.");

        var card = await _cards.GetByIdAsync(redemption.LoyaltyCardId, ct);
        if (card is null)
            return Result.Fail<CancelRedemptionResponse>("No se encontro la tarjeta asociada al canje.");

        var reward = await _rewards.GetByIdAsync(redemption.RewardCatalogItemId, ct);
        var now = _dt.UtcNow;
        var consumptions = await _pointLots.GetActiveConsumptionsByRedemptionIdAsync(redemption.Id, ct);
        if (consumptions.Count == 0)
            return Result.Fail<CancelRedemptionResponse>(
                "El canje no tiene consumos FIFO para restaurar.");

        try
        {
            redemption.Cancel(command.OperatorId, now, command.Notes);
        }
        catch (RedemptionAlreadyConfirmedException)
        {
            return Result.Fail<CancelRedemptionResponse>("Otro operador ya resolvio este canje.");
        }

        card.RestorePoints(redemption.PointsSpent, _dt);
        _cards.Update(card);
        _redemptions.Update(redemption);

        foreach (var consumption in consumptions)
        {
            var lot = await _pointLots.GetLotByIdAsync(consumption.PointLotId, ct);
            if (lot is null)
                return Result.Fail<CancelRedemptionResponse>($"No se encontro el lote {consumption.PointLotId} del canje.");

            lot.Restore(consumption.Amount);
            consumption.MarkReversed(now);
            _pointLots.UpdateLot(lot);
            _pointLots.UpdateConsumption(consumption);
        }

        await _transactions.AddAsync(new PointTransaction(
            id: Guid.NewGuid(),
            tenantId: card.TenantId,
            loyaltyCardId: card.Id,
            points: redemption.PointsSpent,
            type: TransactionType.RedemptionReversal,
            description: $"Cancelacion de canje: {reward?.Name ?? redemption.RewardCatalogItemId.ToString()}",
            createdAtUtc: now,
            createdBy: command.OperatorId), ct);

        await _uow.SaveChangesAsync(ct);
        await TryPushWalletUpdateAsync(card.SerialNumber, ct);

        return Result.Ok(new CancelRedemptionResponse(
            RedemptionId: redemption.Id,
            Status: redemption.Status,
            PointsRestored: redemption.PointsSpent,
            CurrentPoints: card.CurrentPoints,
            CancelledAt: redemption.ConfirmedAt,
            RewardName: reward?.Name));
    }

    private async Task TryPushWalletUpdateAsync(string serial, CancellationToken ct)
    {
        try
        {
            var devices = await _devices.GetBySerialNumberAsync(serial, ct);
            foreach (var device in devices)
                await _apn.SendPassUpdateAsync(device.PushToken, PassUpdateReason.RedemptionCancelled, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo enviando push de Wallet para serial {Serial}", serial);
        }
    }
}
