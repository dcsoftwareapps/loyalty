using KBeauty.Loyalty.Domain.Common;

namespace KBeauty.Loyalty.Domain.Entities;

/// <summary>
/// Una fila <c>Key/Value</c> con una regla del programa. Toda la lógica de
/// negocio lee sus parámetros desde aquí — nunca hardcodea valores.
/// </summary>
/// <remarks>
/// Application normalmente lee todas las filas y construye un
/// <see cref="KBeauty.Loyalty.Domain.ValueObjects.ProgramConfigSnapshot"/> tipado
/// para pasarlo al dominio.
/// </remarks>
public class ProgramConfig : Entity
{
    /// <summary>Clave canónica (ver <c>LoyaltyConstants.ConfigKeys</c>).</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Valor en formato string — el snapshot se encarga del parseo tipado.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>Descripción legible para el panel admin.</summary>
    public string? Description { get; private set; }

    /// <summary>Última fecha (UTC) en que el valor cambió.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Operador que aplicó el último cambio.</summary>
    public string? UpdatedBy { get; private set; }

    private ProgramConfig() { }

    public ProgramConfig(Guid id, string key, string value, DateTime updatedAtUtc, string? description = null, string? updatedBy = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key requerida.", nameof(key));

        Key = key.Trim();
        Value = value ?? string.Empty;
        Description = description?.Trim();
        UpdatedAt = updatedAtUtc;
        UpdatedBy = updatedBy?.Trim();
    }

    /// <summary>Actualiza el valor y la auditoría — la clave es inmutable.</summary>
    public void Update(string value, DateTime updatedAtUtc, string? updatedBy)
    {
        Value = value ?? string.Empty;
        UpdatedAt = updatedAtUtc;
        UpdatedBy = updatedBy?.Trim();
    }
}
