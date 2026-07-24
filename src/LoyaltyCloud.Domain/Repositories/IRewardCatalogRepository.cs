using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Domain.Repositories;

/// <summary>Catalogo de beneficios canjeables.</summary>
public interface IRewardCatalogRepository
{
    /// <summary>Todos los items activos.</summary>
    Task<IReadOnlyList<RewardCatalogItem>> GetAllActiveAsync(CancellationToken ct = default);

    /// <summary>Todos los items del catalogo para administracion, activos e inactivos.</summary>
    Task<IReadOnlyList<RewardCatalogItem>> GetAllAsync(CancellationToken ct = default);

    Task<RewardCatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Producto del mes vigente; null si ninguno esta marcado/vigente.</summary>
    Task<RewardCatalogItem?> GetCurrentMonthlyProductAsync(CancellationToken ct = default);

    Task<bool> HasOverlappingActiveMonthlyProductAsync(
        DateTime validFrom,
        DateTime validTo,
        Guid? excludeRewardId = null,
        CancellationToken ct = default);

    Task AddAsync(RewardCatalogItem item, CancellationToken ct = default);
    void Update(RewardCatalogItem item);
}
