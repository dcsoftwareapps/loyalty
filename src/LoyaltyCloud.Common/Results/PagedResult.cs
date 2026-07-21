using LoyaltyCloud.Common.Pagination;

namespace LoyaltyCloud.Common.Results;

/// <summary>
/// Página de resultados con metadatos de paginación.
/// </summary>
/// <typeparam name="T">Tipo del elemento contenido en cada página.</typeparam>
/// <param name="Items">Elementos de la página actual.</param>
/// <param name="TotalCount">Total de elementos disponibles (no de esta página).</param>
/// <param name="PageNumber">Número de página actual (base 1).</param>
/// <param name="PageSize">Tamaño máximo de página.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    /// <summary>Total de páginas calculado a partir de <see cref="TotalCount"/> y <see cref="PageSize"/>.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Indica si existe una página siguiente.</summary>
    public bool HasNext => PageNumber < TotalPages;

    /// <summary>Indica si existe una página anterior.</summary>
    public bool HasPrevious => PageNumber > 1;

    /// <summary>Página vacía respetando los parámetros recibidos.</summary>
    public static PagedResult<T> Empty(PaginationParams p) =>
        new(Array.Empty<T>(), 0, p.PageNumber, p.PageSize);

    /// <summary>Construye una página a partir de los elementos ya extraídos y el total global.</summary>
    public static PagedResult<T> From(IReadOnlyList<T> items, int totalCount, PaginationParams p) =>
        new(items, totalCount, p.PageNumber, p.PageSize);
}
