using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;

namespace LoyaltyCloud.Application.Redemptions;

internal static class PointLotFifoConsumption
{
    public static async Task ConsumeAsync(
        IPointLotRepository pointLots,
        IReadOnlyList<PointLot> lots,
        int pointsToConsume,
        Guid transactionId,
        Guid redemptionId,
        DateTime now,
        CancellationToken ct)
    {
        var remaining = pointsToConsume;
        foreach (var lot in lots)
        {
            if (remaining == 0)
                break;

            var amount = Math.Min(lot.RemainingAmount, remaining);
            lot.Consume(amount);
            pointLots.UpdateLot(lot);

            await pointLots.AddConsumptionAsync(new PointLotConsumption(
                id: Guid.NewGuid(),
                tenantId: lot.TenantId,
                pointLotId: lot.Id,
                consumingPointTransactionId: transactionId,
                amount: amount,
                createdAtUtc: now,
                redemptionId: redemptionId), ct);

            remaining -= amount;
        }
    }
}
