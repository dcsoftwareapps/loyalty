using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

public class PointCampaign : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int Multiplier { get; private set; }
    public decimal? MinimumPurchaseAmount { get; private set; }
    public CampaignLevelEligibility LevelEligibility { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public DateTime EndsAtUtc { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private PointCampaign() { }

    public PointCampaign(
        Guid id,
        string name,
        string description,
        int multiplier,
        decimal? minimumPurchaseAmount,
        CampaignLevelEligibility levelEligibility,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime createdAtUtc) : base(id)
    {
        Validate(name, description, multiplier, minimumPurchaseAmount, startsAtUtc, endsAtUtc);

        Name = name.Trim();
        Description = description.Trim();
        Multiplier = multiplier;
        MinimumPurchaseAmount = minimumPurchaseAmount;
        LevelEligibility = levelEligibility;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        IsActive = true;
        CreatedAt = createdAtUtc;
    }

    public void Update(
        string name,
        string description,
        int multiplier,
        decimal? minimumPurchaseAmount,
        CampaignLevelEligibility levelEligibility,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime updatedAtUtc)
    {
        Validate(name, description, multiplier, minimumPurchaseAmount, startsAtUtc, endsAtUtc);

        Name = name.Trim();
        Description = description.Trim();
        Multiplier = multiplier;
        MinimumPurchaseAmount = minimumPurchaseAmount;
        LevelEligibility = levelEligibility;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        UpdatedAt = updatedAtUtc;
    }

    public void Activate(DateTime updatedAtUtc)
    {
        IsActive = true;
        UpdatedAt = updatedAtUtc;
    }

    public void Deactivate(DateTime updatedAtUtc)
    {
        IsActive = false;
        UpdatedAt = updatedAtUtc;
    }

    public bool IsCurrentlyActive(DateTime nowUtc) =>
        IsActive && StartsAtUtc <= nowUtc && EndsAtUtc >= nowUtc;

    public bool AppliesToLevel(string level) =>
        LevelEligibility == CampaignLevelEligibility.All ||
        LevelRank(level) >= EligibilityRank(LevelEligibility);

    private static int LevelRank(string level) => level switch
    {
        LoyaltyConstants.Levels.Mist => 1,
        LoyaltyConstants.Levels.Glow => 2,
        LoyaltyConstants.Levels.Radiance => 3,
        _ => 0
    };

    private static int EligibilityRank(CampaignLevelEligibility eligibility) => eligibility switch
    {
        CampaignLevelEligibility.Mist => 1,
        CampaignLevelEligibility.Glow => 2,
        CampaignLevelEligibility.Radiance => 3,
        _ => 0
    };

    private static void Validate(
        string name,
        string description,
        int multiplier,
        decimal? minimumPurchaseAmount,
        DateTime startsAtUtc,
        DateTime endsAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nombre requerido.", nameof(name));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Descripcion requerida.", nameof(description));
        if (multiplier < 2 || multiplier > 5)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "El multiplicador debe estar entre 2 y 5.");
        if (minimumPurchaseAmount.HasValue && minimumPurchaseAmount.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumPurchaseAmount), "El monto minimo no puede ser negativo.");
        if (endsAtUtc < startsAtUtc)
            throw new ArgumentException("La fecha fin no puede ser menor que la fecha inicio.", nameof(endsAtUtc));
    }
}
