namespace KBeauty.Loyalty.Domain.Exceptions;

/// <summary>
/// Base de todas las excepciones del dominio.
/// Reservada para violaciones de invariantes — los flujos esperados (validación,
/// "no encontrado", saldo insuficiente al validar) deben usar <c>Result&lt;T&gt;</c>.
/// </summary>
/// <remarks>
/// El <c>GlobalExceptionHandler</c> de la API mapea estas excepciones a
/// <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> con 400 / 422 según el caso.
/// </remarks>
public abstract class DomainException : Exception
{
    /// <summary>Código corto, estable y legible para el cliente (ej: "INSUFFICIENT_POINTS").</summary>
    public string Code { get; }

    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    protected DomainException(string code, string message, Exception inner) : base(message, inner)
    {
        Code = code;
    }
}
