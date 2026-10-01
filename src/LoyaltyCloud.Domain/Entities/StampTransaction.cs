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
    public long Sequence { get; private set; }
    private StampTransaction() { }

    public static StampTransaction Earn(StampCycle cycle, Guid userId, string key, long sequence, DateTime nowUtc) =>
        Operational(cycle, userId, key, sequence, nowUtc, StampTransactionType.Earn, 1, null);

    public static StampTransaction Reversal(StampCycle cycle, Guid userId, string key, long sequence,
        DateTime nowUtc, StampTransaction earn)
    {
        if (earn.Type != StampTransactionType.Earn || earn.StampCycleId != cycle.Id || earn.TenantId != cycle.TenantId)
            throw new ArgumentException("Only an earn in this cycle can be reversed.");
        return Operational(cycle, userId, key, sequence, nowUtc, StampTransactionType.Reversal, -1, earn.Id);
    }

    private static StampTransaction Operational(StampCycle cycle, Guid userId, string key, long sequence,
        DateTime nowUtc, StampTransactionType type, int delta, Guid? reverses)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > 100 || sequence < 1)
            throw new ArgumentException("Invalid stamp audit data.");
        return new StampTransaction { Id = Guid.NewGuid(), TenantId = cycle.TenantId, StampCycleId = cycle.Id,
            Type = type, Delta = delta, OperatorUserId = userId,
            IdempotencyKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))), Sequence = sequence,
            CreatedAt = nowUtc, ReversesTransactionId = reverses };
    }

    public static StampTransaction Welcome(StampCycle cycle, DateTime nowUtc)
    {
        if (cycle.Sequence != 1 || cycle.CurrentStamps <= 0) throw new ArgumentException("Welcome requires positive first-cycle progress.");
        return new StampTransaction { Id = Guid.NewGuid(), TenantId = cycle.TenantId,
            StampCycleId = cycle.Id, Type = StampTransactionType.Welcome, Delta = cycle.CurrentStamps,
            IdempotencyKey = "welcome", CreatedAt = nowUtc };
    }
}
