using LoyaltyCloud.Application.Redemptions.Queries.ListRedemptions;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Common.Interfaces;

/// <summary>Lectura de historial de canjes para Admin.</summary>
public interface IRedemptionHistoryReadService
{
    Task<IReadOnlyList<RedemptionHistoryItemDto>> ListAsync(
        RedemptionStatus? status,
        string? search,
        CancellationToken ct = default);
}
