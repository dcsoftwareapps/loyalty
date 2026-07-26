using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class BackgroundSchedulerConfigurationTests
{
    [Fact]
    [Trait("Category", "AzureSqlFree")]
    public void Loyalty_maintenance_scheduler_uses_twelve_hour_configured_interval()
    {
        var root = GetRepositoryRoot();
        var options = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.API", "Configuration", "LoyaltyMaintenanceOptions.cs"));
        var service = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.API", "Services", "LoyaltyMaintenanceBackgroundService.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.API", "appsettings.json"));
        var developmentSettings = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.API", "appsettings.Development.json"));

        Assert.Contains("public int IntervalHours { get; init; } = 12;", options);
        Assert.Contains("\"IntervalHours\": 12", appsettings);
        Assert.Contains("\"IntervalHours\": 12", developmentSettings);
        Assert.Contains("TimeSpan.FromHours(options.IntervalHours)", service);
        Assert.Contains("Next loyalty maintenance scheduled in {IntervalHours} hour(s).", service);
        Assert.DoesNotContain("CalculateNextRunUtc", service);
    }

    [Fact]
    [Trait("Category", "AzureSqlFree")]
    public void Notification_processor_keeps_short_poll_interval_for_immediate_deliveries()
    {
        var root = GetRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.API", "Services", "LoyaltyNotificationBackgroundService.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.API", "appsettings.json"));

        Assert.Contains("ProcessDueCustomNotificationCampaignsCommand", service);
        Assert.Contains("ProcessPendingNotificationsCommand", service);
        Assert.Contains("TimeSpan.FromSeconds(Math.Max(options.PollIntervalSeconds", service);
        Assert.Contains("\"PollIntervalSeconds\": 60", appsettings);
        Assert.DoesNotContain("TimeSpan.FromHours(options.IntervalHours)", service);
    }

    [Fact]
    [Trait("Category", "AzureSqlFree")]
    public void Immediate_campaign_and_monthly_product_triggers_remain_connected_to_handlers()
    {
        var root = GetRepositoryRoot();
        var campaignCreate = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Application", "Campaigns", "Commands", "CreatePointCampaign", "CreatePointCampaignHandler.cs"));
        var campaignUpdate = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Application", "Campaigns", "Commands", "UpdatePointCampaign", "UpdatePointCampaignHandler.cs"));
        var rewardCreate = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Application", "Rewards", "Commands", "CreateReward", "CreateRewardHandler.cs"));
        var rewardUpdate = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Application", "Rewards", "Commands", "UpdateReward", "UpdateRewardHandler.cs"));

        Assert.Contains("CreatePointCampaignStartedNotificationsCommand", campaignCreate);
        Assert.Contains("CreatePointCampaignStartedNotificationsCommand", campaignUpdate);
        Assert.Contains("CreateMonthlyProductStartedNotificationsCommand", rewardCreate);
        Assert.Contains("CreateMonthlyProductStartedNotificationsCommand", rewardUpdate);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoyaltyCloud.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
