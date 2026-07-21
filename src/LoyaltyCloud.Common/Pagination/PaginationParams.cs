namespace LoyaltyCloud.Common.Pagination;

/// <summary>
/// Parámetros de paginación para queries. Auto-corrige valores fuera de rango
/// (PageNumber mínimo 1, PageSize entre 1 y <see cref="MaxPageSize"/>).
/// </summary>
public sealed class PaginationParams
{
    /// <summary>Tamaño máximo absoluto permitido para una página (defensa frente a clientes).</summary>
    public const int MaxPageSize = 100;

    /// <summary>Tamaño por defecto cuando el cliente no especifica.</summary>
    public const int DefaultPageSize = 20;

    private int _pageNumber = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>Número de página (base 1). Valores menores a 1 se normalizan a 1.</summary>
    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1 ? 1 : value;
    }

    /// <summary>Tamaño de página. Se acota al rango <c>[1, <see cref="MaxPageSize"/>]</c>.</summary>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1
            ? 1
            : (value > MaxPageSize ? MaxPageSize : value);
    }

    /// <summary>Cantidad de registros a saltar (útil para EF Core <c>.Skip()</c>).</summary>
    public int Skip => (PageNumber - 1) * PageSize;

    /// <summary>Cantidad de registros a tomar (útil para EF Core <c>.Take()</c>).</summary>
    public int Take => PageSize;
}
