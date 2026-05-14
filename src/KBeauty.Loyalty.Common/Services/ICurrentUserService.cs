namespace KBeauty.Loyalty.Common.Services;

/// <summary>
/// Información del usuario autenticado en la request en curso.
/// En la API y en Blazor Admin se resuelve desde el <c>HttpContext</c>;
/// en handlers y servicios se inyecta como dependencia.
/// </summary>
/// <remarks>
/// En el MVP el "usuario" es el dueño de la tienda (único operador con login básico).
/// Cuando crezca el equipo, esta abstracción ya soportará múltiples identidades sin
/// tocar la capa Application.
/// </remarks>
public interface ICurrentUserService
{
    /// <summary>Identificador estable del usuario (o <c>null</c> si la request es anónima).</summary>
    string? UserId { get; }

    /// <summary>Nombre visible del usuario (para auditoría en transacciones / canjes).</summary>
    string? UserName { get; }

    /// <summary>Indica si la request tiene un usuario autenticado.</summary>
    bool IsAuthenticated { get; }
}
