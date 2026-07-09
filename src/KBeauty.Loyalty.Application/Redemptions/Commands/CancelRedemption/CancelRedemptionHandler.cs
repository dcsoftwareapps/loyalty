using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.Exceptions;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Application.Redemptions.Commands.CancelRedemption;

public sealed class CancelRedemptionHandler
    : IRequestHandler<CancelRedemptionCommand, Result<CancelRedemptionResponse>>
{
    private readonly IRedemptionRepository _redemptions;
    private readonly ILoyaltyCardRepository _cards;
    private readonly IRewardCatalogRepository _rewards;
    private readonly IPointTransactionRepository _transactions;
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

        await _transactions.AddAsync(new PointTransaction(
            id: Guid.NewGuid(),
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
