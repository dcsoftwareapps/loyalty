extern alias AdminApp;

using AdminApp::LoyaltyCloud.Admin.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "AdminLocalization")]
public sealed class AdminDateTimeFormatterTests
{
    [Fact]
    public void Date_ShouldConvertUtcAcrossMidnight_ToTijuanaBusinessDate()
    {
        var formatter = new AdminDateTimeFormatter();
        var utc = new DateTime(2026, 9, 1, 2, 0, 0, DateTimeKind.Utc);

        var local = formatter.ToLocal(utc);

        Assert.Equal(new DateTime(2026, 8, 31, 19, 0, 0), local);
        Assert.Contains("31", formatter.Date(utc));
        Assert.Contains("ago", formatter.Date(utc), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Date_ShouldTreatUnspecifiedValuesAsUtc_NotServerLocalTime()
    {
        var formatter = new AdminDateTimeFormatter();
        var unspecifiedUtc = new DateTime(2026, 9, 1, 2, 0, 0, DateTimeKind.Unspecified);

        var local = formatter.ToLocal(unspecifiedUtc);

        Assert.Equal(new DateTime(2026, 8, 31, 19, 0, 0), local);
    }

    [Fact]
    public void NullableFormatters_ShouldReturnFallbackForNullValues()
    {
        var formatter = new AdminDateTimeFormatter();

        Assert.Equal("No disponible", formatter.Date((DateTime?)null));
        Assert.Equal("-", formatter.DateTime((DateTime?)null, "-"));
    }

    [Fact]
    public void LocalDateRange_ShouldConvertTijuanaDateBoundsToUtc()
    {
        var formatter = new AdminDateTimeFormatter();
        var localDate = new DateTime(2026, 8, 31);

        Assert.Equal(new DateTime(2026, 8, 31, 7, 0, 0, DateTimeKind.Utc), formatter.LocalDateStartToUtc(localDate));
        Assert.Equal(new DateTime(2026, 9, 1, 7, 0, 0, DateTimeKind.Utc), formatter.LocalDateExclusiveEndToUtc(localDate));
    }
}
