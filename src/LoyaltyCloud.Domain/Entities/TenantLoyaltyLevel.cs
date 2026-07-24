using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public sealed class TenantLoyaltyLevel : Entity, ITenantOwned
{
    public const int NameMaxLength = 20;

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int Threshold { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private TenantLoyaltyLevel() { }

    public TenantLoyaltyLevel(
        Guid id,
        Guid tenantId,
        string name,
        int threshold,
        int sortOrder,
        DateTime createdAtUtc,
        bool isActive = true) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido.", nameof(tenantId));
        if (threshold < 0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold no puede ser negativo.");
        if (sortOrder < 1)
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "SortOrder debe iniciar en 1.");

        TenantId = tenantId;
        Name = Tenant.Require(name, nameof(name), NameMaxLength);
        NormalizedName = NormalizeName(name);
        Threshold = threshold;
        SortOrder = sortOrder;
        CreatedAt = createdAtUtc;
        IsActive = isActive;
    }

    public void Update(string name, int threshold, int sortOrder, bool isActive, DateTime updatedAtUtc)
    {
        if (threshold < 0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold no puede ser negativo.");
        if (sortOrder < 1)
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "SortOrder debe iniciar en 1.");

        Name = Tenant.Require(name, nameof(name), NameMaxLength);
        NormalizedName = NormalizeName(name);
        Threshold = threshold;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAt = updatedAtUtc;
    }

    public static string NormalizeName(string name) =>
        Tenant.Require(name, nameof(name), NameMaxLength).ToUpperInvariant();
}
