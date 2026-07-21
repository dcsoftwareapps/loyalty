namespace LoyaltyCloud.Domain.Repositories;

/// <summary>
/// Punto único de commit para operaciones que involucran múltiples repositorios
/// dentro de un mismo caso de uso. Cada Command de Application llama
/// <see cref="SaveChangesAsync"/> una sola vez al final.
/// </summary>
/// <remarks>
/// Lo implementa la capa Infrastructure usando <c>DbContext.SaveChangesAsync</c>;
/// como Domain expone el contrato, Application no toca EF Core directamente.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Persiste todos los cambios pendientes en los repositorios. Retorna el
    /// número de filas afectadas (útil para sanity checks en tests).
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
