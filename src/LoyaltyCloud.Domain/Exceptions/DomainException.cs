namespace LoyaltyCloud.Domain.Exceptions;

/// <summary>
/// Base de todas las excepciones del dominio.
/// Reservada para violaciones de invariantes; los flujos esperados deben usar Result.
/// </summary>
/// <remarks>
/// El GlobalExceptionHandler de la API mapea estas excepciones a respuestas ProblemDetails.
/// </remarks>
public abstract class DomainException : Exception
{
    /// <summary>Codigo corto, estable y legible para el cliente.</summary>
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
