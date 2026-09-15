using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Domain.ValueObjects;
using MediatR;

namespace LoyaltyCloud.Application.Redemptions.Queries.PreviewMonetaryRedemption;

public sealed class PreviewMonetaryRedemptionHandler
    : IRequestHandler<PreviewMonetaryRedemptionQuery, Result<MonetaryRedemptionPreviewDto>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly IProgramConfigRepository _config;
    private readonly IPointLotRepository _pointLots;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dt;

    public PreviewMonetaryRedemptionHandler(
        ILoyaltyCardRepository cards,
        IProgramConfigRepository config,
        IPointLotRepository pointLots,
        ITenantContext tenantContext,
        IDateTimeProvider dt)
    {
        _cards = cards;
        _config = config;
        _pointLots = pointLots;
        _tenantContext = tenantContext;
        _dt = dt;
    }

    public async Task<Result<MonetaryRedemptionPreviewDto>> Handle(
        PreviewMonetaryRedemptionQuery query,
        CancellationToken ct)
    {
        var card = await _cards.GetBySerialNumberAsync(query.SerialNumber, ct);
        if (card is null)
            return Result.Fail<MonetaryRedemptionPreviewDto>($"No se encontro tarjeta '{query.SerialNumber}'.");
        if (card.TenantId != _tenantContext.RequireTenantId())
            return Result.Fail<MonetaryRedemptionPreviewDto>("La tarjeta no pertenece al tenant actual.");
        if (!card.IsActive)
            return Result.Fail<MonetaryRedemptionPreviewDto>("La tarjeta esta inactiva.");

        var snapshot = ProgramConfigSnapshot.FromEntries(await _config.GetAllAsync(ct));
        var calculation = MonetaryRedemptionCalculator.Calculate(query.PointsToRedeem, snapshot);
        if (!calculation.IsValid)
            return Result.Fail<MonetaryRedemptionPreviewDto>(calculation.Error!);

        if (card.CurrentPoints < query.PointsToRedeem)
            return Result.Fail<MonetaryRedemptionPreviewDto>(
                $"Saldo insuficiente: necesitas {query.PointsToRedeem} y tienes {card.CurrentPoints}.");

        var lots = await _pointLots.GetAvailableLotsAsync(card.Id, _dt.UtcNow, ct);
        var availableLotPoints = lots.Sum(l => l.RemainingAmount);
        if (availableLotPoints < query.PointsToRedeem)
            return Result.Fail<MonetaryRedemptionPreviewDto>(
                $"Saldo disponible insuficiente: necesitas {query.PointsToRedeem} puntos no vencidos y tienes {availableLotPoints}.");

        return Result.Ok(new MonetaryRedemptionPreviewDto(
            card.SerialNumber,
            calculation.Points,
            calculation.Amount,
            calculation.Currency,
            calculation.PointsPerPesoUnit,
            card.CurrentPoints,
            card.CurrentPoints - calculation.Points));
    }
}
