using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Domain.ValueObjects;
using MediatR;

namespace KBeauty.Loyalty.Application.Redemptions.Queries.GetRedemptionCatalog;

/// <inheritdoc cref="GetRedemptionCatalogQuery"/>
public sealed class GetRedemptionCatalogHandler
    : IRequestHandler<GetRedemptionCatalogQuery, Result<IReadOnlyList<RewardCatalogItemDto>>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly IRewardCatalogRepository _rewards;
    private readonly IPointTransactionRepository _transactions;
    private readonly IProgramConfigRepository _config;
    private readonly ILevelCalculationService _levels;
    private readonly IDateTimeProvider _dt;

    public GetRedemptionCatalogHandler(
        ILoyaltyCardRepository cards,
        IRewardCatalogRepository rewards,
        IPointTransactionRepository transactions,
        IProgramConfigRepository config,
        ILevelCalculationService levels,
        IDateTimeProvider dt)
    {
        _cards = cards;
        _rewards = rewards;
        _transactions = transactions;
        _config = config;
        _levels = levels;
        _dt = dt;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<RewardCatalogItemDto>>> Handle(
        GetRedemptionCatalogQuery query,
        CancellationToken ct)
    {
        var card = await _cards.GetBySerialNumberAsync(query.SerialNumber, ct);
        if (card is null)
            return Result.Fail<IReadOnlyList<RewardCatalogItemDto>>(
                $"No se encontró tarjeta con serial '{query.SerialNumber}'.");

        var snapshot = ProgramConfigSnapshot.FromEntries(await _config.GetAllAsync(ct));
        var now = _dt.UtcNow;
        var rollingPoints = await _transactions.GetEligibleLevelPointsAsync(card.Id, now.AddMonths(-12), ct);
        var level = _levels.CalculateLevel(rollingPoints, snapshot);
        var items = await _rewards.GetByLevelAsync(level, snapshot, ct);

        IReadOnlyList<RewardCatalogItemDto> dtos = items
            .Where(i => i.IsAvailableOn(now))
            .Select(i => new RewardCatalogItemDto(
                Id: i.Id,
                Name: i.Name,
                Description: i.Description,
                PointsCost: i.PointsCost,
                MinLevel: i.MinLevel,
                IsMonthlyProduct: i.IsMonthlyProduct,
                CanAfford: card.CurrentPoints >= i.PointsCost,
                ValidTo: i.ValidTo))
            .ToList()
            .AsReadOnly();

        return Result.Ok(dtos);
    }
}
