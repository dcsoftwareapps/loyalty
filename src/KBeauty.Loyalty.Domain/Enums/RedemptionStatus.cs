namespace KBeauty.Loyalty.Domain.Enums;

/// <summary>Estado del ciclo de vida de un canje.</summary>
public enum RedemptionStatus
{
    /// <summary>La clienta inició el canje; el operador aún no lo entrega.</summary>
    Pending = 0,

    /// <summary>El operador confirmó la entrega del beneficio.</summary>
    Confirmed = 1,

    /// <summary>El canje se canceló y los puntos pueden ser revertidos.</summary>
    Cancelled = 2
}
