using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public enum EarnedStampRewardStatus { Available, Redeemed, Voided }

public sealed class EarnedStampReward : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid StampCycleId { get; private set; }
    public Guid SourceEarnTransactionId { get; private set; }
    public Guid StampRewardDefinitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ShortName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int RequiredStamps { get; private set; }
    public EarnedStampRewardStatus Status { get; private set; }
    public DateTime EarnedAt { get; private set; }
    public DateTime? RedeemedAt { get; private set; }
    public Guid? RedeemedByUserId { get; private set; }
    public DateTime? VoidedAt { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private EarnedStampReward() { }

    public EarnedStampReward(StampCycle cycle, StampTransaction earn, StampRewardDefinition definition, DateTime nowUtc)
        : base(Guid.NewGuid())
    {
        if (cycle.TenantId != definition.TenantId || earn.TenantId != cycle.TenantId
            || earn.StampCycleId != cycle.Id || definition.StampProgramVersionId != cycle.StampProgramVersionId
            || earn.Type != Enums.StampTransactionType.Earn || cycle.CurrentStamps != definition.RequiredStamps)
            throw new ArgumentException("Invalid earned reward transition.");
        TenantId = cycle.TenantId; StampCycleId = cycle.Id; SourceEarnTransactionId = earn.Id;
        StampRewardDefinitionId = definition.Id; ShortName = definition.ShortName; Name = definition.Name; Description = definition.Description;
        RequiredStamps = definition.RequiredStamps; EarnedAt = nowUtc;
    }

    public void Redeem(Guid userId, DateTime nowUtc)
    {
        if (Status != EarnedStampRewardStatus.Available) throw new InvalidOperationException("La recompensa no esta disponible.");
        Status = EarnedStampRewardStatus.Redeemed; RedeemedAt = nowUtc; RedeemedByUserId = userId;
    }

    public void Void(Guid userId, DateTime nowUtc)
    {
        if (Status != EarnedStampRewardStatus.Available) throw new InvalidOperationException("La recompensa no puede anularse.");
        Status = EarnedStampRewardStatus.Voided; VoidedAt = nowUtc; VoidedByUserId = userId;
    }
}
