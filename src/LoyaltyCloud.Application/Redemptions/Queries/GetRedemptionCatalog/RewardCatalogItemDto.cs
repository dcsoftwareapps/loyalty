namespace LoyaltyCloud.Application.Redemptions.Queries.GetRedemptionCatalog;

/// <param name="Id">Identificador de la recompensa.</param>
/// <param name="Name">Nombre visible de la recompensa.</param>
/// <param name="Description">Descripcion visible de la recompensa.</param>
/// <param name="PointsCost">Costo en puntos.</param>
/// <param name="MinLevel">Nivel minimo requerido.</param>
/// <param name="IsMonthlyProduct">Indica si es producto del mes.</param>
/// <param name="CanAfford">True si el saldo actual de la clienta alcanza para este beneficio.</param>
/// <param name="ValidTo">Fecha limite de vigencia, si existe.</param>
public sealed record RewardCatalogItemDto(
    Guid Id,
    string Name,
    string Description,
    int PointsCost,
    string MinLevel,
    bool IsMonthlyProduct,
    bool CanAfford,
    DateTime? ValidTo);
