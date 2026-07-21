using LoyaltyCloud.Common.Pagination;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Domain.Repositories;

/// <summary>Diario de movimientos de puntos por tarjeta.</summary>
public interface IPointTransactionRepository
{
    /// <summary>Historial paginado de una tarjeta, ordenado por <c>CreatedAt</c> desc.</summary>
    Task<PagedResult<PointTransaction>> GetByCardIdAsync(
        Guid loyaltyCardId,
        PaginationParams pagination,
        CancellationToken ct = default);

    /// <summary>
    /// Suma de puntos positivos ganados en los últimos 12 meses desde
    /// <c>dt.UtcNow</c> — para verificar re-cualificación Radiance.
    /// </summary>
    Task<int> GetPointsEarnedThisYearAsync(
        Guid loyaltyCardId,
        IDateTimeProvider dt,
        CancellationToken ct = default);

    Task<int> GetEligibleLevelPointsAsync(
        Guid loyaltyCardId,
        DateTime windowStartUtc,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, int>> GetEligibleLevelPointsByCardAsync(
        DateTime windowStartUtc,
        CancellationToken ct = default);

    Task AddAsync(PointTransaction transaction, CancellationToken ct = default);
}
