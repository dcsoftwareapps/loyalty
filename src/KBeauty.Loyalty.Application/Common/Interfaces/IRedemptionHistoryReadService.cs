using KBeauty.Loyalty.Application.Redemptions.Queries.ListRedemptions;
using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Application.Common.Interfaces;

/// <summary>Lectura de historial de canjes para Admin.</summary>
public interface IRedemptionHistoryReadService
{
    Task<IReadOnlyList<RedemptionHistoryItemDto>> ListAsync(
        RedemptionStatus? status,
        string? search,
        CancellationToken ct = default);
}
