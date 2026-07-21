namespace LoyaltyCloud.API.Configuration;

public sealed class CustomNotificationCampaignOptions
{
    public const string SectionName = "CustomNotificationCampaigns";

    public int BatchSize { get; init; } = 50;
}
