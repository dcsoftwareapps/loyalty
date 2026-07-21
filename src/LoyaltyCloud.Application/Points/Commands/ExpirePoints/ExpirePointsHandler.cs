using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Points.Commands.ExpirePoints;

public sealed class ExpirePointsHandler : IRequestHandler<ExpirePointsCommand, Result<ExpirePointsResponse>>
{
    private readonly IPointLotRepository _pointLots;
    private readonly ILoyaltyCardRepository _cards;
    private readonly IPointTransactionRepository _transactions;
    private readonly IProgramConfigRepository _config;
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IApnService _apn;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ExpirePointsHandler> _logger;

    public ExpirePointsHandler(
        IPointLotRepository pointLots,
        ILoyaltyCardRepository cards,
        IPointTransactionRepository transactions,
        IProgramConfigRepository config,
        IDeviceRegistrationRepository devices,
        IApnService apn,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ILogger<ExpirePointsHandler> logger)
    {
        _pointLots = pointLots;
        _cards = cards;
        _transactions = transactions;
        _config = config;
        _devices = devices;
        _apn = apn;
        _dt = dt;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<ExpirePointsResponse>> Handle(ExpirePointsCommand command, CancellationToken ct)
    {
        var now = _dt.UtcNow;
        var snapshot = ProgramConfigSnapshot.FromEntries(await _config.GetAllAsync(ct));
        if (snapshot.PointsExpireAfterMonths <= 0)
            return Result.Fail<ExpirePointsResponse>("points_expire_after_months debe ser mayor a 0.");

        if (!snapshot.PointsExpirationEnabled)
        {
            return Result.Ok(new ExpirePointsResponse(
                RunAt: now,
                Enabled: false,
                ClientsProcessed: 0,
                ClientsAffected: 0,
                LotsExpired: 0,
                PointsExpired: 0,
                WalletsNotified: 0,
                Warnings: Array.Empty<string>()));
        }

        var expiredLots = await _pointLots.GetExpiredLotsAsync(now, ct);
        var warnings = new List<string>();
        var clientsProcessed = expiredLots.Select(l => l.LoyaltyCardId).Distinct().Count();
        var clientsAffected = 0;
        var lotsExpired = 0;
        var pointsExpired = 0;
        var walletsNotified = 0;
        var affectedSerials = new List<string>();

        foreach (var group in expiredLots.GroupBy(l => l.LoyaltyCardId))
        {
            var card = await _cards.GetByIdAsync(group.Key, ct);
            if (card is null)
            {
                warnings.Add($"No se encontro la tarjeta {group.Key} para lotes vencidos.");
                continue;
            }

            var requestedExpiration = group.Sum(l => l.RemainingAmount);
            var amountToExpire = Math.Min(requestedExpiration, card.CurrentPoints);
            if (amountToExpire <= 0)
            {
                warnings.Add($"La tarjeta {card.SerialNumber} tiene lotes vencidos pero saldo disponible 0.");
                continue;
            }

            if (requestedExpiration > card.CurrentPoints)
            {
                warnings.Add(
                    $"La tarjeta {card.SerialNumber} tiene {requestedExpiration} pts vencidos en lotes, " +
                    $"pero solo {card.CurrentPoints} pts disponibles. Se expiraron {amountToExpire} pts.");
            }

            var transactionId = Guid.NewGuid();
            await _transactions.AddAsync(new PointTransaction(
                id: transactionId,
                tenantId: card.TenantId,
                loyaltyCardId: card.Id,
                points: -amountToExpire,
                type: TransactionType.Expired,
                description: "Expiracion automatica de puntos",
                createdAtUtc: now,
                createdBy: command.OperatorId), ct);

            var remaining = amountToExpire;
            foreach (var lot in group.OrderBy(l => l.ExpiresAt).ThenBy(l => l.EarnedAt).ThenBy(l => l.Id))
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
                    createdAtUtc: now), ct);

                remaining -= amount;
                lotsExpired++;
            }

            card.ExpirePoints(amountToExpire, _dt);
            _cards.Update(card);
            clientsAffected++;
            pointsExpired += amountToExpire;
            affectedSerials.Add(card.SerialNumber);
        }

        await _uow.SaveChangesAsync(ct);

        foreach (var serial in affectedSerials.Distinct(StringComparer.OrdinalIgnoreCase))
            walletsNotified += await TryPushWalletUpdateAsync(serial, ct);

        return Result.Ok(new ExpirePointsResponse(
            RunAt: now,
            Enabled: true,
            ClientsProcessed: clientsProcessed,
            ClientsAffected: clientsAffected,
            LotsExpired: lotsExpired,
            PointsExpired: pointsExpired,
            WalletsNotified: walletsNotified,
            Warnings: warnings.AsReadOnly()));
    }

    private async Task<int> TryPushWalletUpdateAsync(string serial, CancellationToken ct)
    {
        try
        {
            var sent = 0;
            var devices = await _devices.GetBySerialNumberAsync(serial, ct);
            foreach (var device in devices)
            {
                await _apn.SendPassUpdateAsync(device.PushToken, PassUpdateReason.PointsExpired, ct);
                sent++;
            }

            return sent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo enviando push de Wallet por expiracion para serial {Serial}", serial);
            return 0;
        }
    }
}
