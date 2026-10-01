using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

public sealed class StampProgramVersion : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public int Version { get; private set; }
    public int MaxStamps { get; private set; }
    public int WelcomeStamps { get; private set; }
    public StampReversalPolicy ReversalPolicy { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime? SupersededAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private StampProgramVersion() { }

    public StampProgramVersion(Guid id, Guid tenantId, int version, int maxStamps,
        int welcomeStamps, StampReversalPolicy reversalPolicy, Guid createdByUserId, DateTime nowUtc) : base(id)
    {
        if (tenantId == Guid.Empty || createdByUserId == Guid.Empty) throw new ArgumentException("Tenant and operator are required.");
        if (version < 1 || maxStamps < 1 || welcomeStamps < 0 || welcomeStamps >= maxStamps)
            throw new ArgumentOutOfRangeException(nameof(welcomeStamps));
        if (!Enum.IsDefined(reversalPolicy)) throw new ArgumentOutOfRangeException(nameof(reversalPolicy));
        TenantId = tenantId; Version = version; MaxStamps = maxStamps; WelcomeStamps = welcomeStamps;
        ReversalPolicy = reversalPolicy; CreatedByUserId = createdByUserId; CreatedAt = nowUtc; IsActive = true;
    }

    public void Supersede(DateTime nowUtc) { IsActive = false; SupersededAt ??= nowUtc; }
}
