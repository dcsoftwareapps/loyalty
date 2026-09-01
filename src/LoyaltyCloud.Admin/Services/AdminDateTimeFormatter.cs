using System.Globalization;

namespace LoyaltyCloud.Admin.Services;

public sealed class AdminDateTimeFormatter
{
    private const string DefaultTimeZoneId = "America/Tijuana";
    private static readonly CultureInfo SpanishMexico = CultureInfo.GetCultureInfo("es-MX");
    private readonly TimeZoneInfo _timeZone;

    public AdminDateTimeFormatter()
    {
        _timeZone = ResolveTimeZone(DefaultTimeZoneId);
    }

    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.UtcNow, _timeZone);

    public DateTime LocalToday => LocalNow.Date;

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), _timeZone);

    public DateTime ToLocal(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, _timeZone).DateTime;

    public string Date(DateTime utc, string fallback = "No disponible") =>
        Format(utc, "dd MMM yyyy", fallback);

    public string Date(DateTime? utc, string fallback = "No disponible") =>
        utc.HasValue ? Date(utc.Value, fallback) : fallback;

    public string DateNumeric(DateTime utc, string fallback = "No disponible") =>
        Format(utc, "dd/MM/yyyy", fallback);

    public string DateNumeric(DateTime? utc, string fallback = "No disponible") =>
        utc.HasValue ? DateNumeric(utc.Value, fallback) : fallback;

    public string DateTime(DateTime utc, string fallback = "No disponible") =>
        Format(utc, "dd MMM yyyy HH:mm", fallback);

    public string DateTime(DateTime? utc, string fallback = "No disponible") =>
        utc.HasValue ? DateTime(utc.Value, fallback) : fallback;

    public string DateTimeWithComma(DateTime utc, string fallback = "No disponible") =>
        Format(utc, "dd MMM yyyy, HH:mm", fallback);

    public string DateTimeInput(DateTime? utc) =>
        utc.HasValue ? ToLocal(utc.Value).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) : string.Empty;

    public string DateInput(DateTime? utc) =>
        utc.HasValue ? ToLocal(utc.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;

    public DateTime LocalDateStartToUtc(DateTime localDate)
    {
        var local = System.DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, _timeZone);
    }

    public DateTime LocalDateExclusiveEndToUtc(DateTime localDate) =>
        LocalDateStartToUtc(localDate.Date.AddDays(1));

    public DateTime LocalDateTimeToUtc(DateTime localDateTime)
    {
        var local = System.DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, _timeZone);
    }

    private string Format(DateTime utc, string format, string fallback) =>
        utc == default ? fallback : ToLocal(utc).ToString(format, SpanishMexico);

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : System.DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time (Mexico)");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time (Mexico)");
        }
    }
}
