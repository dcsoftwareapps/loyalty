namespace KBeauty.Loyalty.Domain.Enums;

/// <summary>Naturaleza de un movimiento en el saldo de puntos de una clienta.</summary>
public enum TransactionType
{
    /// <summary>Compra en tienda física — suma puntos según ratio configurado.</summary>
    Purchase = 0,

    /// <summary>Bono de bienvenida al registrarse.</summary>
    BonusWelcome = 1,

    /// <summary>Bono x2 durante el mes de cumpleaños.</summary>
    BonusBirthday = 2,

    /// <summary>Bono recibido por traer a una nueva clienta.</summary>
    BonusReferral = 3,

    /// <summary>Canje de un beneficio del catálogo — resta puntos.</summary>
    Redemption = 4,

    /// <summary>Expiración o ajuste manual a la baja.</summary>
    Expiry = 5
}
