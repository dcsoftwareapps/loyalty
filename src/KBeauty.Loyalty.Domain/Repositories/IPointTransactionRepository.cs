using KBeauty.Loyalty.Common.Pagination;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;

namespace KBeauty.Loyalty.Domain.Repositories;

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

    Task AddAsync(PointTransaction transaction, CancellationToken ct = default);
}
