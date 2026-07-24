using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Application.Rewards;
using MediatR;

namespace LoyaltyCloud.Application.Redemptions.Queries.GetRedemptionCatalog;

/// <inheritdoc cref="GetRedemptionCatalogQuery"/>
public sealed class GetRedemptionCatalogHandler
    : IRequestHandler<GetRedemptionCatalogQuery, Result<IReadOnlyList<RewardCatalogItemDto>>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly IRewardCatalogRepository _rewards;
    private readonly IPointTransactionRepository _transactions;
    private readonly ILevelCalculationService _levels;
    private readonly ITenantLoyaltyLevelReadService _tenantLevels;
    private readonly IDateTimeProvider _dt;

    public GetRedemptionCatalogHandler(
        ILoyaltyCardRepository cards,
        IRewardCatalogRepository rewards,
        IPointTransactionRepository transactions,
        ILevelCalculationService levels,
        ITenantLoyaltyLevelReadService tenantLevels,
        IDateTimeProvider dt)
    {
        _cards = cards;
        _rewards = rewards;
        _transactions = transactions;
        _levels = levels;
        _tenantLevels = tenantLevels;
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

        var tenantLevels = await _tenantLevels.GetActiveLevelsAsync(ct);
        var now = _dt.UtcNow;
        var rollingPoints = await _transactions.GetEligibleLevelPointsAsync(card.Id, now.AddMonths(-12), ct);
        var level = _levels.CalculateLevel(rollingPoints, tenantLevels);
        var items = await _rewards.GetAllActiveAsync(ct);

        IReadOnlyList<RewardCatalogItemDto> dtos = items
            .Where(i => RewardLevelRules.IsEligible(level, i.MinLevel, tenantLevels, _levels))
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
