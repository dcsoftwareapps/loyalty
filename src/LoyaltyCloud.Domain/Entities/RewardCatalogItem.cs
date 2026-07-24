using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.ValueObjects;

namespace LoyaltyCloud.Domain.Entities;

/// <summary>
/// Item del catalogo de canjes. El costo y nivel minimo se editan desde el panel admin.
/// </summary>
public class RewardCatalogItem : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>Costo en puntos al momento del canje.</summary>
    public int PointsCost { get; private set; }

    /// <summary>Nombre del nivel minimo requerido para canjear. Vacio significa todos los niveles.</summary>
    public string MinLevel { get; private set; } = string.Empty;

    /// <summary>Si esta activo en el catalogo publico.</summary>
    public bool IsActive { get; private set; }

    /// <summary>El Producto del Mes rotativo.</summary>
    public bool IsMonthlyProduct { get; private set; }

    /// <summary>Vigencia opcional inicio.</summary>
    public DateTime? ValidFrom { get; private set; }

    /// <summary>Vigencia opcional fin.</summary>
    public DateTime? ValidTo { get; private set; }

    private RewardCatalogItem() { }

    public RewardCatalogItem(
        Guid id,
        Guid tenantId,
        string name,
        string description,
        int pointsCost,
        string minLevel,
        bool isMonthlyProduct = false,
        DateTime? validFrom = null,
        DateTime? validTo = null) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nombre requerido.", nameof(name));
        if (pointsCost <= 0)
            throw new ArgumentOutOfRangeException(nameof(pointsCost), "Costo debe ser positivo.");

        TenantId = tenantId;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        PointsCost = pointsCost;
        MinLevel = minLevel?.Trim() ?? string.Empty;
        IsActive = true;
        IsMonthlyProduct = isMonthlyProduct;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    /// <summary>Indica si el item es canjeable hoy considerando IsActive y vigencias.</summary>
    public bool IsAvailableOn(DateTime nowUtc)
    {
        if (!IsActive) return false;
        if (IsMonthlyProduct && (!ValidFrom.HasValue || !ValidTo.HasValue)) return false;
        if (ValidFrom.HasValue && nowUtc < ValidFrom.Value) return false;
        if (ValidTo.HasValue && nowUtc > ValidTo.Value) return false;
        return true;
    }

    /// <summary>Determina si la clienta con <paramref name="customerLevel"/> puede canjear este item.</summary>
    public bool IsEligibleFor(MemberLevel customerLevel, MemberLevel? requiredLevel) =>
        requiredLevel is null || customerLevel.IsAtLeast(requiredLevel);

    /// <summary>Actualiza costo, nivel minimo y vigencia desde el panel admin.</summary>
    public void Update(
        string name,
        string description,
        int pointsCost,
        string minLevel,
        bool isMonthlyProduct,
        DateTime? validFrom,
        DateTime? validTo)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nombre requerido.", nameof(name));
        if (pointsCost <= 0) throw new ArgumentOutOfRangeException(nameof(pointsCost));

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        PointsCost = pointsCost;
        MinLevel = minLevel?.Trim() ?? string.Empty;
        IsMonthlyProduct = isMonthlyProduct;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
