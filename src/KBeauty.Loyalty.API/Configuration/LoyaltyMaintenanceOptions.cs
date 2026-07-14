namespace KBeauty.Loyalty.API.Configuration;

public sealed class LoyaltyMaintenanceOptions
{
    public const string SectionName = "LoyaltyMaintenance";

    public bool Enabled { get; init; } = true;
    public bool RunOnStartup { get; init; }
    public string RunAtLocalTime { get; init; } = "02:00";
    public string TimeZoneId { get; init; } = "America/Tijuana";
}
