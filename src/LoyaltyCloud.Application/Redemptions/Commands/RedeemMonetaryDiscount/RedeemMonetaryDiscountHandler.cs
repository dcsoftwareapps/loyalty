using LoyaltyCloud.Application.Common.Events;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Redemptions.Commands.RedeemReward;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Events;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Redemptions.Commands.RedeemMonetaryDiscount;

public sealed class RedeemMonetaryDiscountHandler
    : IRequestHandler<RedeemMonetaryDiscountCommand, Result<RedemptionResponse>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly IProgramConfigRepository _config;
    private readonly IRedemptionRepository _redemptions;
    private readonly IPointTransactionRepository _transactions;
    private readonly IPointLotRepository _pointLots;
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IApnService _apn;
    private readonly ITenantContext _tenantContext;
    private readonly IPublisher _publisher;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RedeemMonetaryDiscountHandler> _logger;

    public RedeemMonetaryDiscountHandler(
        ILoyaltyCardRepository cards,
        IProgramConfigRepository config,
        IRedemptionRepository redemptions,
        IPointTransactionRepository transactions,
        IPointLotRepository pointLots,
        IDeviceRegistrationRepository devices,
        IApnService apn,
        ITenantContext tenantContext,
        IPublisher publisher,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ILogger<RedeemMonetaryDiscountHandler> logger)
    {
        _cards = cards;
        _config = config;
        _redemptions = redemptions;
        _transactions = transactions;
        _pointLots = pointLots;
        _devices = devices;
        _apn = apn;
        _tenantContext = tenantContext;
        _publisher = publisher;
        _dt = dt;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<RedemptionResponse>> Handle(RedeemMonetaryDiscountCommand command, CancellationToken ct)
    {
        var card = await _cards.GetBySerialNumberAsync(command.SerialNumber, ct);
        if (card is null)
            return Result.Fail<RedemptionResponse>($"No se encontro tarjeta '{command.SerialNumber}'.");
        if (card.TenantId != _tenantContext.RequireTenantId())
            return Result.Fail<RedemptionResponse>("La tarjeta no pertenece al tenant actual.");
        if (!card.IsActive)
            return Result.Fail<RedemptionResponse>("La tarjeta esta inactiva.");

        var snapshot = ProgramConfigSnapshot.FromEntries(await _config.GetAllAsync(ct));
        var calculation = MonetaryRedemptionCalculator.Calculate(command.PointsToRedeem, snapshot);
        if (!calculation.IsValid)
            return Result.Fail<RedemptionResponse>(calculation.Error!);

        if (card.CurrentPoints < command.PointsToRedeem)
            return Result.Fail<RedemptionResponse>(
                $"Saldo insuficiente: necesitas {command.PointsToRedeem} y tienes {card.CurrentPoints}.");

        var now = _dt.UtcNow;
        var lots = await _pointLots.GetAvailableLotsAsync(card.Id, now, ct);
        var availableLotPoints = lots.Sum(l => l.RemainingAmount);
        if (availableLotPoints < command.PointsToRedeem)
            return Result.Fail<RedemptionResponse>(
                $"Saldo disponible insuficiente: necesitas {command.PointsToRedeem} puntos no vencidos y tienes {availableLotPoints}.");

        card.RedeemPoints(command.PointsToRedeem);
        card.Touch(_dt);
        _cards.Update(card);

        var redemption = new Redemption(
            id: Guid.NewGuid(),
            tenantId: card.TenantId,
            loyaltyCardId: card.Id,
            pointsSpent: command.PointsToRedeem,
            monetaryAmount: calculation.Amount,
            monetaryCurrency: calculation.Currency,
            pointsPerPesoUnit: calculation.PointsPerPesoUnit,
            redeemedAtUtc: now);
        await _redemptions.AddAsync(redemption, ct);

        var transactionId = Guid.NewGuid();
        await _transactions.AddAsync(new PointTransaction(
            id: transactionId,
            tenantId: card.TenantId,
            loyaltyCardId: card.Id,
            points: -command.PointsToRedeem,
            type: TransactionType.Redemption,
            description: $"Canje: Descuento en dinero ${calculation.Amount:N2} {calculation.Currency}",
            createdAtUtc: now,
            createdBy: command.OperatorId), ct);

        await PointLotFifoConsumption.ConsumeAsync(_pointLots, lots, command.PointsToRedeem, transactionId, redemption.Id, now, ct);

        await _uow.SaveChangesAsync(ct);

        await _publisher.Publish(
            new DomainEventNotification<RedemptionRequestedEvent>(
                new RedemptionRequestedEvent(redemption.Id, card.Id, "Descuento en dinero", command.PointsToRedeem)),
            ct);

        await TryPushWalletUpdateAsync(card.SerialNumber, ct);

        return Result.Ok(new RedemptionResponse(
            RedemptionId: redemption.Id,
            RewardName: "Descuento en dinero",
            PointsSpent: command.PointsToRedeem,
            RemainingPoints: card.CurrentPoints,
            Status: redemption.Status,
            RedeemedAt: redemption.RedeemedAt,
            MonetaryAmount: calculation.Amount,
            MonetaryCurrency: calculation.Currency,
            MonetaryPointsPerPesoUnit: calculation.PointsPerPesoUnit));
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
