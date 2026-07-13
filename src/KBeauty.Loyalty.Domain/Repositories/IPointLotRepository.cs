using KBeauty.Loyalty.Domain.Entities;

namespace KBeauty.Loyalty.Domain.Repositories;

/// <summary>Acceso a lotes de puntos y consumos FIFO.</summary>
public interface IPointLotRepository
{
    Task AddLotAsync(PointLot lot, CancellationToken ct = default);
    Task AddConsumptionAsync(PointLotConsumption consumption, CancellationToken ct = default);

    Task<IReadOnlyList<PointLot>> GetAvailableLotsAsync(
        Guid loyaltyCardId,
        DateTime nowUtc,
        CancellationToken ct = default);

    Task<IReadOnlyList<PointLotConsumption>> GetActiveConsumptionsByRedemptionIdAsync(
        Guid redemptionId,
        CancellationToken ct = default);

    Task<PointLot?> GetLotByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<PointLot>> GetExpiredLotsAsync(DateTime nowUtc, CancellationToken ct = default);

    void UpdateLot(PointLot lot);
    void UpdateConsumption(PointLotConsumption consumption);
}
