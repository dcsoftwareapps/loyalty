using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

public sealed class StampTransaction : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid StampCycleId { get; private set; }
    public StampTransactionType Type { get; private set; }
    public int Delta { get; private set; }
    public Guid? OperatorUserId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public Guid? ReversesTransactionId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    private StampTransaction() { }

    public static StampTransaction Welcome(StampCycle cycle, DateTime nowUtc)
    {
        if (cycle.Sequence != 1 || cycle.CurrentStamps <= 0) throw new ArgumentException("Welcome requires positive first-cycle progress.");
        return new StampTransaction { Id = Guid.NewGuid(), TenantId = cycle.TenantId,
            StampCycleId = cycle.Id, Type = StampTransactionType.Welcome, Delta = cycle.CurrentStamps,
            IdempotencyKey = "welcome", CreatedAt = nowUtc };
    }
}
