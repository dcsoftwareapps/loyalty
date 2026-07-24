using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public class PointCampaign : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int Multiplier { get; private set; }
    public decimal? MinimumPurchaseAmount { get; private set; }
    public string LevelEligibility { get; private set; } = CampaignLevelEligibilityAll;
    public DateTime StartsAtUtc { get; private set; }
    public DateTime EndsAtUtc { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private PointCampaign() { }

    public PointCampaign(
        Guid id,
        Guid tenantId,
        string name,
        string description,
        int multiplier,
        decimal? minimumPurchaseAmount,
        string levelEligibility,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime createdAtUtc) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido.", nameof(tenantId));
        Validate(name, description, multiplier, minimumPurchaseAmount, startsAtUtc, endsAtUtc);

        TenantId = tenantId;
        Name = name.Trim();
        Description = description.Trim();
        Multiplier = multiplier;
        MinimumPurchaseAmount = minimumPurchaseAmount;
        LevelEligibility = NormalizeLevelEligibility(levelEligibility);
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
        string levelEligibility,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime updatedAtUtc)
    {
        Validate(name, description, multiplier, minimumPurchaseAmount, startsAtUtc, endsAtUtc);

        Name = name.Trim();
        Description = description.Trim();
        Multiplier = multiplier;
        MinimumPurchaseAmount = minimumPurchaseAmount;
        LevelEligibility = NormalizeLevelEligibility(levelEligibility);
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

    public bool AppliesToLevel(string level, IReadOnlyDictionary<string, int> levelRanks)
    {
        if (IsAllLevels(LevelEligibility))
            return true;

        return levelRanks.TryGetValue(level, out var customerRank)
            && levelRanks.TryGetValue(LevelEligibility, out var requiredRank)
            && customerRank >= requiredRank;
    }

    public static string CampaignLevelEligibilityAll => "All";

    public static bool IsAllLevels(string? value) =>
        string.Equals(value, CampaignLevelEligibilityAll, StringComparison.OrdinalIgnoreCase);

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

    private static string NormalizeLevelEligibility(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return CampaignLevelEligibilityAll;

        return value.Trim();
    }
}
