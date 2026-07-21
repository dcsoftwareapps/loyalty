namespace LoyaltyCloud.Common.Extensions;

/// <summary>
/// Extensiones de <see cref="DateTime"/> para reglas temporales del programa de lealtad.
/// </summary>
/// <remarks>
/// Todas las extensiones reciben explícitamente la fecha "ahora" para ser puras y testeables.
/// En código productivo, "ahora" llega vía <c>IDateTimeProvider</c>.
/// </remarks>
public static class DateTimeExtensions
{
    /// <summary>
    /// Determina si el mes de la fecha de nacimiento coincide con el mes de <paramref name="now"/>.
    /// Usado para aplicar el bono x2 en mes de cumpleaños.
    /// </summary>
    /// <param name="dob">Fecha de nacimiento de la clienta.</param>
    /// <param name="now">Fecha "ahora" inyectada (UTC o local, debe ser consistente con dob).</param>
    public static bool IsBirthMonth(this DateTime dob, DateTime now) =>
        dob.Date != new DateTime(1900, 1, 1) && dob.Month == now.Month;

    /// <summary>
    /// Determina si la fecha cae dentro del último año respecto a <paramref name="now"/>.
    /// Usado para la lógica de re-cualificación Radiance (500 pts por año).
    /// </summary>
    /// <param name="date">Fecha a evaluar.</param>
    /// <param name="now">Fecha "ahora" inyectada.</param>
    public static bool IsWithinLastYear(this DateTime date, DateTime now) =>
        date <= now && date >= now.AddYears(-1);
}
