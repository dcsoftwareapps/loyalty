using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Common;
using KBeauty.Loyalty.Domain.ValueObjects;

namespace KBeauty.Loyalty.Domain.Entities;

/// <summary>
/// Ítem del catálogo de canjes (mini producto, $50 off, FocusSkin, etc.).
/// El costo y nivel mínimo se editan desde el panel admin sin desplegar código.
/// </summary>
public class RewardCatalogItem : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>Costo en puntos al momento del canje.</summary>
    public int PointsCost { get; private set; }

    /// <summary>Nivel mínimo para canjear (ver <see cref="LoyaltyConstants.Levels"/>).</summary>
    public string MinLevel { get; private set; } = LoyaltyConstants.Levels.Mist;

    /// <summary>Si está activo en el catálogo público.</summary>
    public bool IsActive { get; private set; }

    /// <summary>El "Producto del Mes" rotativo — solo uno activo a la vez.</summary>
    public bool IsMonthlyProduct { get; private set; }

    /// <summary>Vigencia opcional inicio (útil para promociones temporales).</summary>
    public DateTime? ValidFrom { get; private set; }

    /// <summary>Vigencia opcional fin.</summary>
    public DateTime? ValidTo { get; private set; }

    private RewardCatalogItem() { }

    public RewardCatalogItem(
        Guid id,
        string name,
        string description,
        int pointsCost,
        string minLevel,
        bool isMonthlyProduct = false,
        DateTime? validFrom = null,
        DateTime? validTo = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nombre requerido.", nameof(name));
        if (pointsCost <= 0)
            throw new ArgumentOutOfRangeException(nameof(pointsCost), "Costo debe ser positivo.");
        if (string.IsNullOrWhiteSpace(minLevel))
            throw new ArgumentException("MinLevel requerido.", nameof(minLevel));

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        PointsCost = pointsCost;
        MinLevel = minLevel.Trim();
        IsActive = true;
        IsMonthlyProduct = isMonthlyProduct;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    /// <summary>Indica si el ítem es canjeable hoy considerando IsActive y vigencias.</summary>
    public bool IsAvailableOn(DateTime nowUtc)
    {
        if (!IsActive) return false;
        if (IsMonthlyProduct && (!ValidFrom.HasValue || !ValidTo.HasValue)) return false;
        if (ValidFrom.HasValue && nowUtc < ValidFrom.Value) return false;
        if (ValidTo.HasValue && nowUtc > ValidTo.Value) return false;
        return true;
    }

    /// <summary>Determina si la clienta con <paramref name="customerLevel"/> puede canjear este ítem.</summary>
    public bool IsEligibleFor(MemberLevel customerLevel, ProgramConfigSnapshot config) =>
        customerLevel.IsAtLeast(MinLevel, config);

    /// <summary>Actualiza costo / nivel mínimo / vigencia desde el panel admin.</summary>
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
        if (string.IsNullOrWhiteSpace(minLevel)) throw new ArgumentException("MinLevel requerido.", nameof(minLevel));

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        PointsCost = pointsCost;
        MinLevel = minLevel.Trim();
        IsMonthlyProduct = isMonthlyProduct;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
