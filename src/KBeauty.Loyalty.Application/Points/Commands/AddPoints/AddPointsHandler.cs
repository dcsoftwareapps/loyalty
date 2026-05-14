using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Extensions;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Application.Points.Commands.AddPoints;

/// <inheritdoc cref="AddPointsCommand"/>
public sealed class AddPointsHandler : IRequestHandler<AddPointsCommand, Result<AddPointsResponse>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly ICustomerRepository _customers;
    private readonly IPointTransactionRepository _transactions;
    private readonly IProgramConfigRepository _config;
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IApnService _apn;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AddPointsHandler> _logger;

    public AddPointsHandler(
        ILoyaltyCardRepository cards,
        ICustomerRepository customers,
        IPointTransactionRepository transactions,
        IProgramConfigRepository config,
        IDeviceRegistrationRepository devices,
        IApnService apn,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ILogger<AddPointsHandler> logger)
    {
        _cards = cards;
        _customers = customers;
        _transactions = transactions;
        _config = config;
        _devices = devices;
        _apn = apn;
        _dt = dt;
        _uow = uow;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<AddPointsResponse>> Handle(AddPointsCommand command, CancellationToken ct)
    {
        var card = await _cards.GetBySerialNumberAsync(command.SerialNumber, ct);
        if (card is null)
            return Result.Fail<AddPointsResponse>($"No se encontró tarjeta con serial '{command.SerialNumber}'.");
        if (!card.IsActive)
            return Result.Fail<AddPointsResponse>("La tarjeta está inactiva.");

        var customer = await _customers.GetByIdAsync(card.CustomerId, ct);
        if (customer is null || !customer.IsActive)
            return Result.Fail<AddPointsResponse>("La clienta no está activa.");

        // Cálculo de puntos
        var snapshot = ProgramConfigSnapshot.FromEntries(await _config.GetAllAsync(ct));
        var basePoints = snapshot.CalculatePointsForPurchase(command.PurchaseAmount);
        if (basePoints <= 0)
            return Result.Fail<AddPointsResponse>(
                $"El monto ${command.PurchaseAmount:0.00} es muy bajo para generar puntos.");

        var isBirthMonth = customer.DateOfBirth.IsBirthMonth(_dt.UtcNow);
        var finalPoints = isBirthMonth ? basePoints * snapshot.BirthdayMultiplier : basePoints;
        var bonusType = isBirthMonth ? BonusType.Birthday : (BonusType?)null;

        // Aplicar a la tarjeta (raises domain events)
        var oldLevel = card.Level;
        card.EarnPoints(finalPoints, TransactionType.Purchase, snapshot, _dt);
        var leveledUp = !string.Equals(oldLevel, card.Level, StringComparison.Ordinal);
        _cards.Update(card);

        // Registrar la transacción en el diario
        var description = isBirthMonth
            ? $"Compra ${command.PurchaseAmount:0.00} (x{snapshot.BirthdayMultiplier} cumpleaños)"
            : $"Compra ${command.PurchaseAmount:0.00}";

        await _transactions.AddAsync(new PointTransaction(
            id: Guid.NewGuid(),
            loyaltyCardId: card.Id,
            points: finalPoints,
            type: TransactionType.Purchase,
            description: description,
            createdAtUtc: _dt.UtcNow,
            bonusType: bonusType,
            purchaseAmount: command.PurchaseAmount,
            createdBy: command.OperatorId), ct);

        await _uow.SaveChangesAsync(ct);

        // Push a Wallet — best-effort, no falla la transacción si el APN está caído.
        await TryPushWalletUpdateAsync(card.SerialNumber,
            leveledUp ? PassUpdateReason.LevelChanged : PassUpdateReason.PointsAdded, ct);

        return Result.Ok(new AddPointsResponse(
            PointsAdded: finalPoints,
            NewTotal: card.CurrentPoints,
            Level: card.Level,
            LeveledUp: leveledUp,
            BirthdayBonusApplied: isBirthMonth));
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
}
