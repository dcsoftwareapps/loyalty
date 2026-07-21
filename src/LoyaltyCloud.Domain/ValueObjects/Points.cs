namespace LoyaltyCloud.Domain.ValueObjects;

/// <summary>
/// Value object para cantidad de puntos. Garantiza no-negatividad y soporta
/// operadores aritméticos / de comparación. Útil cuando se quiere seguridad
/// de tipo en cálculos del dominio.
/// </summary>
/// <remarks>
/// Las entidades persisten el saldo como <c>int</c> simple para mapeo directo
/// a SQL; <c>Points</c> se usa en parámetros de método, validaciones y
/// pruebas para evitar errores como "puntos negativos".
/// </remarks>
public readonly record struct Points : IComparable<Points>
{
    /// <summary>Valor numérico interno.</summary>
    public int Value { get; }

    /// <summary>Construye un <see cref="Points"/>. Lanza si <paramref name="value"/> es negativo.</summary>
    public Points(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Los puntos no pueden ser negativos.");
        Value = value;
    }

    /// <summary>Instancia con cero puntos.</summary>
    public static Points Zero => new(0);

    public static Points operator +(Points a, Points b) => new(a.Value + b.Value);

    /// <summary>Resta — lanza si el resultado sería negativo.</summary>
    public static Points operator -(Points a, Points b) => new(a.Value - b.Value);

    public static bool operator >(Points a, Points b) => a.Value > b.Value;
    public static bool operator <(Points a, Points b) => a.Value < b.Value;
    public static bool operator >=(Points a, Points b) => a.Value >= b.Value;
    public static bool operator <=(Points a, Points b) => a.Value <= b.Value;

    /// <summary>Conversión implícita desde <see cref="int"/> — los ints positivos son válidos como puntos.</summary>
    public static implicit operator Points(int value) => new(value);

    /// <summary>Conversión implícita hacia <see cref="int"/> — facilita comparaciones e interop con EF Core.</summary>
    public static implicit operator int(Points points) => points.Value;

    public int CompareTo(Points other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}
