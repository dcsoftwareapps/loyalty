namespace LoyaltyCloud.Domain.Exceptions;

/// <summary>
/// Una operación de canje intentó descontar más puntos de los disponibles
/// en la tarjeta. Indica violación de invariante — el validator de la
/// capa Application debió haber detectado esto antes.
/// </summary>
public sealed class InsufficientPointsException : DomainException
{
    /// <summary>Puntos requeridos por la operación.</summary>
    public int Required { get; }

    /// <summary>Saldo disponible en la tarjeta al momento de la operación.</summary>
    public int Available { get; }

    public InsufficientPointsException(int required, int available)
        : base("INSUFFICIENT_POINTS",
              $"Puntos insuficientes: se requieren {required} y el saldo es {available}.")
    {
        Required = required;
        Available = available;
    }
}
