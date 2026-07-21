namespace LoyaltyCloud.Application.Redemptions.Queries.GetRedemptionCatalog;

/// <param name="CanAfford">True si el saldo actual de la clienta alcanza para este beneficio.</param>
public sealed record RewardCatalogItemDto(
    Guid Id,
    string Name,
    string Description,
    int PointsCost,
    string MinLevel,
    bool IsMonthlyProduct,
    bool CanAfford,
    DateTime? ValidTo);
