using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.ValueObjects;

namespace KBeauty.Loyalty.Domain.Repositories;

/// <summary>Catálogo de beneficios canjeables.</summary>
public interface IRewardCatalogRepository
{
    /// <summary>Todos los ítems activos (sin filtrar por nivel — útil para el panel admin).</summary>
    Task<IReadOnlyList<RewardCatalogItem>> GetAllActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Ítems elegibles para una clienta de cierto <see cref="MemberLevel"/>:
    /// activos, dentro de vigencia, con <c>MinLevel</c> ≤ nivel de la clienta.
    /// </summary>
    Task<IReadOnlyList<RewardCatalogItem>> GetByLevelAsync(
        MemberLevel level,
        ProgramConfigSnapshot config,
        CancellationToken ct = default);

    Task<RewardCatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Producto del mes vigente — null si ninguno está marcado/vigente.</summary>
    Task<RewardCatalogItem?> GetCurrentMonthlyProductAsync(CancellationToken ct = default);

    Task AddAsync(RewardCatalogItem item, CancellationToken ct = default);
    void Update(RewardCatalogItem item);
}
