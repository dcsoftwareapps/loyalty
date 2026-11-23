using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public sealed class StampRewardDefinition : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid StampProgramVersionId { get; private set; }
    public Guid RewardIdentityId { get; private set; }
    public int RequiredStamps { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public const int ShortNameMaxLength = 40;
    public string ShortName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    private StampRewardDefinition() { }

    public StampRewardDefinition(Guid id, StampProgramVersion program, int requiredStamps,
        string name, string? description, Guid? rewardIdentityId = null, string? shortName = null) : base(id)
    {
        if (rewardIdentityId == Guid.Empty) throw new ArgumentException("Reward identity cannot be empty.", nameof(rewardIdentityId));
        if (requiredStamps < 1 || requiredStamps > program.MaxStamps) throw new ArgumentOutOfRangeException(nameof(requiredStamps));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150 || description?.Length > 1000)
            throw new ArgumentException("Invalid reward text.");
        TenantId = program.TenantId; StampProgramVersionId = program.Id; RequiredStamps = requiredStamps;
        Name = name.Trim(); Description = description;
        ShortName = NormalizeShortName(shortName, Name);
        RewardIdentityId = rewardIdentityId ?? Guid.NewGuid();
    }
    public static string NormalizeShortName(string? shortName, string name)
    {
        // Omission preserves compatibility with existing API clients and historical data.
        if (shortName is null) return name.Trim()[..Math.Min(name.Trim().Length, ShortNameMaxLength)];
        var value = shortName.Trim();
        if (value.Length is < 1 or > ShortNameMaxLength)
            throw new ArgumentException("El nombre corto debe tener entre 1 y 40 caracteres.");
        return value;
    }
}
