namespace LoyaltyCloud.Domain.ValueObjects;

/// <summary>
/// Value object para montos monetarios con moneda explicita.
/// Evita confundir MXN con USD en calculos de puntos.
/// </summary>
public sealed record Money
{
    /// <summary>Moneda nativa de la tienda.</summary>
    public const string DefaultCurrency = "MXN";

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "El monto no puede ser negativo.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Moneda requerida.", nameof(currency));

        Amount = Math.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency.ToUpperInvariant();
    }

    /// <summary>Monto cero en la moneda por defecto.</summary>
    public static Money Zero => new(0m);

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (!string.Equals(a.Currency, b.Currency, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"No se pueden operar montos en monedas distintas ({a.Currency} vs {b.Currency}).");
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
