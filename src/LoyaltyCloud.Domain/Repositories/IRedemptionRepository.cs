using LoyaltyCloud.Common.Pagination;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Domain.Repositories;

/// <summary>Acceso al ciclo de vida de los canjes.</summary>
public interface IRedemptionRepository
{
    Task<Redemption?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Historial de canjes de una tarjeta, paginado.</summary>
    Task<PagedResult<Redemption>> GetByCardIdAsync(
        Guid loyaltyCardId,
        PaginationParams pagination,
        CancellationToken ct = default);

    /// <summary>Lista de canjes en estado <c>Pending</c> (el panel admin necesita confirmar).</summary>
    Task<IReadOnlyList<Redemption>> GetPendingAsync(CancellationToken ct = default);

    Task AddAsync(Redemption redemption, CancellationToken ct = default);
    void Update(Redemption redemption);
}
