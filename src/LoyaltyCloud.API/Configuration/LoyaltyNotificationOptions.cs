namespace LoyaltyCloud.API.Configuration;

public sealed class LoyaltyNotificationOptions
{
    public const string SectionName = "LoyaltyNotifications";

    public bool Enabled { get; init; } = true;
    public bool RunOnStartup { get; init; }
    public int PollIntervalSeconds { get; init; } = 43_200;
    public int BatchSize { get; init; } = 25;
    public int MaxAttempts { get; init; } = 3;
    public int VisibleEventPriorityHours { get; init; } = 24;
}
