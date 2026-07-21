namespace LoyaltyCloud.Domain.Enums;

/// <summary>
/// Clasificador secundario opcional para transacciones con bono o multiplicador.
/// Una <c>Purchase</c> normal lleva <c>BonusType = null</c>; si la misma compra
/// recibió x2 por cumpleaños, lleva <c>BonusType = Birthday</c>.
/// </summary>
public enum BonusType
{
    /// <summary>Bono de bienvenida al registro.</summary>
    Welcome = 1,

    /// <summary>Multiplicador por mes de cumpleaños.</summary>
    Birthday = 2,

    /// <summary>Bono por referido.</summary>
    Referral = 3,

    /// <summary>Ajuste o regalo manual del operador.</summary>
    Manual = 4
}
