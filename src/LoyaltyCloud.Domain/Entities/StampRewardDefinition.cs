using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public sealed class StampRewardDefinition : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid StampProgramVersionId { get; private set; }
    public int RequiredStamps { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    private StampRewardDefinition() { }

    public StampRewardDefinition(Guid id, StampProgramVersion program, int requiredStamps,
        string name, string? description) : base(id)
    {
        if (requiredStamps < 1 || requiredStamps > program.MaxStamps) throw new ArgumentOutOfRangeException(nameof(requiredStamps));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150 || description?.Length > 1000)
            throw new ArgumentException("Invalid reward text.");
        TenantId = program.TenantId; StampProgramVersionId = program.Id; RequiredStamps = requiredStamps;
        Name = name.Trim(); Description = description;
    }
}
