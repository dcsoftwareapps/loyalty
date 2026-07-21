using System.Security.Cryptography;
using System.Text;

namespace LoyaltyCloud.Common.Extensions;

/// <summary>
/// Extensiones de <see cref="string"/> usadas a través de la solución.
/// </summary>
public static class StringExtensions
{
    // Alfabeto sin caracteres ambiguos (sin 0/O/I/1) para serials legibles a mano.
    private static readonly char[] SerialAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    /// <summary>
    /// Genera un número de serie determinista en formato <c>KB-XXXXXXX</c> (7 chars)
    /// a partir de una semilla arbitraria. Usado para serials de LoyaltyCard
    /// derivados del CustomerId.
    /// </summary>
    /// <param name="seed">Semilla (por ejemplo, un <see cref="Guid"/> formateado).</param>
    /// <returns>Serial con prefijo <c>KB-</c> seguido de 7 caracteres del alfabeto seguro.</returns>
    public static string ToSerialNumber(this string seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);

        Span<byte> hash = stackalloc byte[20];
        SHA1.HashData(Encoding.UTF8.GetBytes(seed), hash);

        var sb = new StringBuilder("KB-", capacity: 10);
        for (int i = 0; i < 7; i++)
        {
            sb.Append(SerialAlphabet[hash[i] % SerialAlphabet.Length]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Trunca la cadena a <paramref name="maxLength"/> caracteres. Devuelve cadena
    /// vacía si el valor es <c>null</c>.
    /// </summary>
    /// <param name="value">Cadena a truncar (puede ser null).</param>
    /// <param name="maxLength">Longitud máxima deseada. Si es ≤ 0 retorna vacío.</param>
    public static string Truncate(this string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
