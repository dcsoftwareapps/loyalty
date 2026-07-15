using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Application.Levels.Commands.RecalculateLevels;

public sealed class RecalculateLevelsHandler
    : IRequestHandler<RecalculateLevelsCommand, Result<RecalculateLevelsResponse>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly IPointTransactionRepository _transactions;
    private readonly IProgramConfigRepository _config;
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IApnService _apn;
    private readonly ILevelCalculationService _levels;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RecalculateLevelsHandler> _logger;

    public RecalculateLevelsHandler(
        ILoyaltyCardRepository cards,
        IPointTransactionRepository transactions,
        IProgramConfigRepository config,
        IDeviceRegistrationRepository devices,
        IApnService apn,
        ILevelCalculationService levels,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ILogger<RecalculateLevelsHandler> logger)
    {
        _cards = cards;
        _transactions = transactions;
        _config = config;
        _devices = devices;
        _apn = apn;
        _levels = levels;
        _dt = dt;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<RecalculateLevelsResponse>> Handle(
        RecalculateLevelsCommand command,
        CancellationToken ct)
    {
        var now = _dt.UtcNow;
        var windowStart = now.AddMonths(-12);
        var snapshot = ProgramConfigSnapshot.FromEntries(await _config.GetAllAsync(ct));
        var cards = await _cards.GetActiveAsync(ct);
        var pointsByCard = await _transactions.GetEligibleLevelPointsByCardAsync(windowStart, ct);
        var warnings = new List<string>();
        var affectedSerials = new List<string>();
        var cardsChanged = 0;
        var cardsUpgraded = 0;
        var cardsDowngraded = 0;

        foreach (var card in cards)
        {
            var rollingPoints = pointsByCard.TryGetValue(card.Id, out var points) ? points : 0;
            var calculatedLevel = _levels.CalculateLevel(rollingPoints, snapshot);
            var comparison = _levels.CompareLevels(card.Level, calculatedLevel.Name, snapshot);

            if (!card.ApplyCalculatedLevel(calculatedLevel, _dt))
                continue;

            _cards.Update(card);
            cardsChanged++;

            if (comparison > 0)
            {
                cardsUpgraded++;
            }
            else if (comparison < 0)
            {
                cardsDowngraded++;
                affectedSerials.Add(card.SerialNumber);
            }
        }

        await _uow.SaveChangesAsync(ct);

        var walletsNotified = 0;
        foreach (var serial in affectedSerials.Distinct(StringComparer.OrdinalIgnoreCase))
            walletsNotified += await TryPushWalletUpdateAsync(serial, warnings, ct);

        return Result.Ok(new RecalculateLevelsResponse(
            RunAt: now,
            CardsProcessed: cards.Count,
            CardsChanged: cardsChanged,
            CardsUpgraded: cardsUpgraded,
            CardsDowngraded: cardsDowngraded,
            WalletsNotified: walletsNotified,
            Warnings: warnings.AsReadOnly()));
    }

    private async Task<int> TryPushWalletUpdateAsync(
        string serial,
        List<string> warnings,
        CancellationToken ct)
    {
        try
        {
            var sent = 0;
            var devices = await _devices.GetBySerialNumberAsync(serial, ct);
            foreach (var device in devices)
            {
                await _apn.SendPassUpdateAsync(device.PushToken, PassUpdateReason.LevelChanged, ct);
                sent++;
            }

            return sent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo enviando push de Wallet por recalculo de nivel para serial {Serial}", serial);
            warnings.Add($"No se pudo notificar Wallet para serial {serial}.");
            return 0;
        }
    }
}
