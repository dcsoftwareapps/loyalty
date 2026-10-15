using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

// Stores the committed response so a lost HTTP response can be replayed after rollover.
public sealed class StampOperationReceipt : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public string SerialNumber { get; private set; } = string.Empty;
    public Guid? RewardId { get; private set; }
    public Guid OperatorUserId { get; private set; }
    public string ResponseJson { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    private StampOperationReceipt() { }

    public StampOperationReceipt(Guid tenantId, string key, string operation, string serial,
        Guid? rewardId, Guid userId, string responseJson, DateTime nowUtc) : base(Guid.NewGuid())
    {
        TenantId = tenantId; IdempotencyKey = key; Operation = operation; SerialNumber = serial;
        RewardId = rewardId; OperatorUserId = userId; ResponseJson = responseJson; CreatedAt = nowUtc;
    }
}
