using KBeauty.Loyalty.Domain.Entities;

namespace KBeauty.Loyalty.Domain.Repositories;

/// <summary>Acceso al diccionario configurable de reglas del programa.</summary>
public interface IProgramConfigRepository
{
    /// <summary>Lee una clave puntual; <c>null</c> si no existe.</summary>
    Task<ProgramConfig?> GetByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>Todas las filas — Application las pasa a <c>ProgramConfigSnapshot.FromEntries</c>.</summary>
    Task<IReadOnlyList<ProgramConfig>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Inserta la clave si no existe; si existe, actualiza su valor y auditoría.
    /// El commit lo hace <c>IUnitOfWork</c>.
    /// </summary>
    Task UpsertAsync(string key, string value, string? updatedBy, CancellationToken ct = default);
}
