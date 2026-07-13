namespace KBeauty.Loyalty.Domain.Enums;

/// <summary>
/// Tipos de transaccion que cuentan para calcular nivel en ventana movil de 12 meses.
/// Canjes, reversas y expiraciones quedan excluidos.
/// </summary>
public static class LevelProgressTransactionTypes
{
    public static readonly TransactionType[] All =
    [
        TransactionType.Purchase,
        TransactionType.BonusWelcome,
        TransactionType.BonusBirthday,
        TransactionType.BonusReferral
    ];

    public static bool Contains(TransactionType type) => All.Contains(type);
}
