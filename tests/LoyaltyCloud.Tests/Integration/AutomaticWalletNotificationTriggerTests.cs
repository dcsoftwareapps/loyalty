using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyCloud.Application.Campaigns.Commands.CreatePointCampaign;
using LoyaltyCloud.Application.Campaigns.Commands.UpdatePointCampaign;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.Commands.CreateMonthlyProductStartedNotifications;
using LoyaltyCloud.Application.Notifications.Commands.CreatePointCampaignStartedNotifications;
using LoyaltyCloud.Application.Rewards.Commands.CreateReward;
using LoyaltyCloud.Application.Rewards.Commands.UpdateReward;
using LoyaltyCloud.Common.Security;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class AutomaticWalletNotificationTriggerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private const string SharedSecret = "test-admin-api-shared-secret-with-enough-length";
    private const string Serial = "KB-AUTO-NOTIFY";
    private const string PushToken = "push-auto-notify";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AutomaticWalletNotificationTriggerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        _factory.Apn.Calls.Clear();
        _factory.Apn.FailSends = false;
        _factory.Apn.NextResult = null;
        await ResetDataAsync();
        await SeedWalletCustomerAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Creating_active_current_point_campaign_sends_immediate_wallet_notification()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentCampaign("Immediate campaign")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, await CountNotificationsAsync(NotificationType.PointCampaignStarted));
        Assert.Equal(1, await CountDeliveriesAsync(NotificationType.PointCampaignStarted, NotificationDeliveryStatus.Succeeded));
        Assert.Contains(_factory.Apn.Calls, call => call.Token == PushToken);
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Point_campaign_wallet_message_uses_campaign_name_and_multiplier()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentCampaign("Freee")));
        Assert.True(result.IsSuccess, result.Error);

        var context = await GetWalletNotificationContextAsync();
        var finalVisibleMessage = $"{context.PointCampaign!.ChangeMessage.Replace("%@", context.PointCampaign.Value, StringComparison.Ordinal)}";

        Assert.Equal("Freee \u00b7 Gana puntos x2", context.PointCampaign.Value);
        Assert.Equal("\ud83c\udf89 %@", context.PointCampaign.ChangeMessage);
        Assert.Equal("\ud83c\udf89 Freee \u00b7 Gana puntos x2", finalVisibleMessage);
        Assert.NotEqual("Promoci\u00f3n activa \u00b7 Puntos x2", finalVisibleMessage);
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Creating_current_point_campaign_sends_directed_notification_even_when_another_campaign_is_better()
    {
        var better = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(new CreatePointCampaignCommand(
            "Better campaign",
            "Better global campaign.",
            3,
            null,
            PointCampaign.CampaignLevelEligibilityAll,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddHours(2),
            true)));
        Assert.True(better.IsSuccess, better.Error);

        var poolParty = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(new CreatePointCampaignCommand(
            "Pool Party",
            "Directed campaign with lower benefit.",
            2,
            100m,
            PointCampaign.CampaignLevelEligibilityAll,
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(2),
            true)));

        Assert.True(poolParty.IsSuccess, poolParty.Error);
        Assert.Equal(1, await CountCampaignNotificationsAsync(poolParty.Value.Id));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Creating_future_point_campaign_waits_for_scheduler_window()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateFutureCampaign("Future campaign")));
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, await CountNotificationsAsync(NotificationType.PointCampaignStarted));

        await MoveCampaignIntoCurrentWindowAsync(result.Value.Id);

        var scheduler = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(
            new CreatePointCampaignStartedNotificationsCommand("scheduler-test")));

        Assert.True(scheduler.IsSuccess, scheduler.Error);
        Assert.Equal(1, await CountNotificationsAsync(NotificationType.PointCampaignStarted));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Signed_admin_campaign_create_request_runs_in_api_and_sends_immediate_notification()
    {
        using var request = CreateSignedRequest(
            HttpMethod.Post,
            "/api/campaigns",
            new
            {
                name = "HTTP Pool Party",
                description = "Created through signed Admin API.",
                multiplier = 2,
                minimumPurchaseAmount = 100m,
                levelEligibility = PointCampaign.CampaignLevelEligibilityAll,
                startsAtUtc = DateTime.UtcNow.AddMinutes(-5),
                endsAtUtc = DateTime.UtcNow.AddHours(2),
                isActive = true
            });

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var campaign = await response.Content.ReadFromJsonAsync<LoyaltyCloud.Application.Campaigns.PointCampaignAdminDto>();
        Assert.NotNull(campaign);
        Assert.Equal(1, await CountCampaignNotificationsAsync(campaign!.Id));
        Assert.Contains(_factory.Apn.Calls, call => call.Token == PushToken);
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Point_campaign_scheduler_does_not_duplicate_after_immediate_notification()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentCampaign("No duplicate campaign")));
        Assert.True(result.IsSuccess, result.Error);

        var scheduler = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(
            new CreatePointCampaignStartedNotificationsCommand("scheduler-test")));

        Assert.True(scheduler.IsSuccess, scheduler.Error);
        Assert.Equal(1, await CountNotificationsAsync(NotificationType.PointCampaignStarted));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Editing_already_notified_current_point_campaign_does_not_duplicate_notification()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentCampaign("Edit campaign")));
        Assert.True(result.IsSuccess, result.Error);

        var update = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(new UpdatePointCampaignCommand(
            result.Value.Id,
            "Edit campaign renamed",
            "Updated campaign.",
            3,
            null,
            PointCampaign.CampaignLevelEligibilityAll,
            DateTime.UtcNow.AddMinutes(-15),
            DateTime.UtcNow.AddHours(2),
            true)));

        Assert.True(update.IsSuccess, update.Error);
        Assert.Equal(1, await CountNotificationsAsync(NotificationType.PointCampaignStarted));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Activating_inactive_current_point_campaign_sends_immediate_wallet_notification()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(new CreatePointCampaignCommand(
            "Inactive current campaign",
            "Current campaign initially inactive.",
            2,
            null,
            PointCampaign.CampaignLevelEligibilityAll,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddHours(2),
            false)));
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, await CountNotificationsAsync(NotificationType.PointCampaignStarted));

        var update = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(new UpdatePointCampaignCommand(
            result.Value.Id,
            result.Value.Name,
            result.Value.Description,
            result.Value.Multiplier,
            result.Value.MinimumPurchaseAmount,
            result.Value.LevelEligibility,
            result.Value.StartsAtUtc,
            result.Value.EndsAtUtc,
            true)));

        Assert.True(update.IsSuccess, update.Error);
        Assert.Equal(1, await CountNotificationsAsync(NotificationType.PointCampaignStarted));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Apns_failure_does_not_revert_current_point_campaign_creation()
    {
        _factory.Apn.FailSends = true;

        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentCampaign("APNs fail campaign")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(await CampaignExistsAsync(result.Value.Id));
        Assert.Equal(1, await CountCampaignNotificationsAsync(result.Value.Id));
        Assert.Equal(1, await CountDeliveriesAsync(NotificationType.PointCampaignStarted, NotificationDeliveryStatus.Failed));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Transient_apns_failure_is_retried_without_duplicate_delivery()
    {
        _factory.Apn.NextResult = ApnPushResult.Transient(429, "TooManyRequests");
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentCampaign("Retry transient campaign")));
        Assert.True(result.IsSuccess, result.Error);

        var failed = await GetSingleDeliveryAsync(NotificationType.PointCampaignStarted);
        Assert.Equal(NotificationDeliveryStatus.Failed, failed.Status);
        Assert.Equal(1, failed.AttemptCount);
        Assert.Contains("Transient APNs failure", failed.FailureReason);

        await AgeSingleDeliveryAsync(NotificationType.PointCampaignStarted, DateTime.UtcNow.AddMinutes(-2));
        _factory.Apn.NextResult = ApnPushResult.Accepted(200);

        var processed = await WithTenantAsync(sp => sp.GetRequiredService<ILoyaltyNotificationService>().ProcessPendingAsync(25, 3));

        Assert.Equal(1, processed);
        var retried = await GetSingleDeliveryAsync(NotificationType.PointCampaignStarted);
        Assert.Equal(NotificationDeliveryStatus.Succeeded, retried.Status);
        Assert.Equal(2, retried.AttemptCount);
        Assert.Equal(1, await CountDeliveriesTotalAsync(NotificationType.PointCampaignStarted));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Permanent_apns_failure_is_not_retried_by_scheduler()
    {
        _factory.Apn.NextResult = ApnPushResult.Permanent(400, "BadDeviceToken");
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentCampaign("Permanent failure campaign")));
        Assert.True(result.IsSuccess, result.Error);

        await AgeSingleDeliveryAsync(NotificationType.PointCampaignStarted, DateTime.UtcNow.AddMinutes(-20));
        _factory.Apn.NextResult = ApnPushResult.Accepted(200);

        var processed = await WithTenantAsync(sp => sp.GetRequiredService<ILoyaltyNotificationService>().ProcessPendingAsync(25, 3));

        Assert.Equal(0, processed);
        var delivery = await GetSingleDeliveryAsync(NotificationType.PointCampaignStarted);
        Assert.Equal(NotificationDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Contains("Permanent APNs failure", delivery.FailureReason);
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Noop_apns_is_recorded_as_unsupported_not_success()
    {
        _factory.Apn.NextResult = ApnPushResult.Unsupported("APNs real deshabilitado por configuracion.");

        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentCampaign("NoOp campaign")));

        Assert.True(result.IsSuccess, result.Error);
        var delivery = await GetSingleDeliveryAsync(NotificationType.PointCampaignStarted);
        Assert.Equal(NotificationDeliveryStatus.Unsupported, delivery.Status);
        Assert.Equal(0, delivery.PushesAccepted);
        Assert.Equal(1, delivery.PushesFailed);
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Stuck_processing_notification_is_recovered_by_scheduler()
    {
        var notificationId = await SeedStuckProcessingNotificationAsync();

        var processed = await WithTenantAsync(sp => sp.GetRequiredService<ILoyaltyNotificationService>().ProcessPendingAsync(25, 3));

        Assert.Equal(1, processed);
        var delivery = await GetSingleDeliveryAsync(NotificationType.Custom);
        Assert.Equal(NotificationDeliveryStatus.Succeeded, delivery.Status);
        Assert.Equal(2, delivery.AttemptCount);
        Assert.Single(_factory.Apn.Calls, call => call.Token == PushToken);
        Assert.True(await WithTenantAsync(sp => sp.GetRequiredService<AppDbContext>()
            .LoyaltyNotifications
            .AnyAsync(n => n.Id == notificationId && n.Status == NotificationStatus.Delivered)));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Creating_active_current_monthly_product_sends_immediate_wallet_notification()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentMonthlyProduct("Immediate monthly product")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, await CountNotificationsAsync(NotificationType.MonthlyProductStarted));
        Assert.Equal(1, await CountDeliveriesAsync(NotificationType.MonthlyProductStarted, NotificationDeliveryStatus.Succeeded));
        Assert.Contains(_factory.Apn.Calls, call => call.Token == PushToken);
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Creating_current_monthly_product_sends_directed_notification_for_that_reward()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentMonthlyProduct("Directed monthly product")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, await CountMonthlyProductNotificationsAsync(result.Value.Id));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Creating_future_monthly_product_waits_for_scheduler_window()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateFutureMonthlyProduct("Future monthly product")));
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, await CountNotificationsAsync(NotificationType.MonthlyProductStarted));

        await MoveRewardIntoCurrentWindowAsync(result.Value.Id);

        var scheduler = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(
            new CreateMonthlyProductStartedNotificationsCommand("scheduler-test")));

        Assert.True(scheduler.IsSuccess, scheduler.Error);
        Assert.Equal(1, await CountNotificationsAsync(NotificationType.MonthlyProductStarted));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Monthly_product_scheduler_does_not_duplicate_after_immediate_notification()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentMonthlyProduct("No duplicate monthly product")));
        Assert.True(result.IsSuccess, result.Error);

        var scheduler = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(
            new CreateMonthlyProductStartedNotificationsCommand("scheduler-test")));

        Assert.True(scheduler.IsSuccess, scheduler.Error);
        Assert.Equal(1, await CountNotificationsAsync(NotificationType.MonthlyProductStarted));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Editing_already_notified_current_monthly_product_does_not_duplicate_notification()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentMonthlyProduct("Edit monthly product")));
        Assert.True(result.IsSuccess, result.Error);

        var update = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(new UpdateRewardCommand(
            result.Value.Id,
            "Edit monthly product renamed",
            "Updated monthly product.",
            100,
            string.Empty,
            true,
            DateTime.UtcNow.AddMinutes(-15),
            DateTime.UtcNow.AddHours(2),
            true)));

        Assert.True(update.IsSuccess, update.Error);
        Assert.Equal(1, await CountNotificationsAsync(NotificationType.MonthlyProductStarted));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Activating_inactive_current_monthly_product_sends_immediate_wallet_notification()
    {
        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(new CreateRewardCommand(
            "Inactive monthly product",
            "Current monthly product initially inactive.",
            100,
            string.Empty,
            true,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddHours(2),
            false)));
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, await CountNotificationsAsync(NotificationType.MonthlyProductStarted));

        var update = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(new UpdateRewardCommand(
            result.Value.Id,
            result.Value.Name,
            result.Value.Description,
            result.Value.PointsCost,
            result.Value.MinLevel,
            true,
            result.Value.ValidFrom,
            result.Value.ValidTo,
            true)));

        Assert.True(update.IsSuccess, update.Error);
        Assert.Equal(1, await CountNotificationsAsync(NotificationType.MonthlyProductStarted));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public async Task Apns_failure_does_not_revert_current_monthly_product_creation()
    {
        _factory.Apn.FailSends = true;

        var result = await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(CreateCurrentMonthlyProduct("APNs fail monthly product")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(await RewardExistsAsync(result.Value.Id));
        Assert.Equal(1, await CountMonthlyProductNotificationsAsync(result.Value.Id));
        Assert.Equal(1, await CountDeliveriesAsync(NotificationType.MonthlyProductStarted, NotificationDeliveryStatus.Failed));
    }

    [Fact]
    [Trait("Category", "AutomaticWalletNotifications")]
    public void Admin_campaigns_and_rewards_use_api_for_mutations_that_can_trigger_apns()
    {
        var root = GetRepositoryRoot();
        var campaigns = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "Campaigns.razor"));
        var rewards = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "Rewards.razor"));

        Assert.Contains("AdminApiClient", campaigns);
        Assert.Contains("/api/campaigns", campaigns);
        Assert.DoesNotContain("CreatePointCampaignCommand", campaigns);
        Assert.DoesNotContain("UpdatePointCampaignCommand", campaigns);
        Assert.DoesNotContain("ActivatePointCampaignCommand", campaigns);

        Assert.Contains("AdminApiClient", rewards);
        Assert.Contains("/api/rewards", rewards);
        Assert.DoesNotContain("CreateRewardCommand", rewards);
        Assert.DoesNotContain("UpdateRewardCommand", rewards);
        Assert.DoesNotContain("ActivateRewardCommand", rewards);
    }

    private static CreatePointCampaignCommand CreateCurrentCampaign(string name) =>
        new(
            name,
            "Active current campaign.",
            2,
            null,
            PointCampaign.CampaignLevelEligibilityAll,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddHours(2),
            true);

    private static CreatePointCampaignCommand CreateFutureCampaign(string name) =>
        new(
            name,
            "Future campaign.",
            2,
            null,
            PointCampaign.CampaignLevelEligibilityAll,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2),
            true);

    private static CreateRewardCommand CreateCurrentMonthlyProduct(string name) =>
        new(
            name,
            "Active current monthly product.",
            100,
            string.Empty,
            true,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddHours(2),
            true);

    private static CreateRewardCommand CreateFutureMonthlyProduct(string name) =>
        new(
            name,
            "Future monthly product.",
            100,
            string.Empty,
            true,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2),
            true);

    private async Task ResetDataAsync()
    {
        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();

            db.NotificationDeliveries.RemoveRange(db.NotificationDeliveries);
            db.LoyaltyNotifications.RemoveRange(db.LoyaltyNotifications);
            db.DeviceRegistrations.RemoveRange(db.DeviceRegistrations.Where(d => d.SerialNumber == Serial));
            db.LoyaltyCards.RemoveRange(db.LoyaltyCards.Where(c => c.SerialNumber == Serial));
            db.Customers.RemoveRange(db.Customers.Where(c => c.Email == "auto-notify@test.local"));
            db.PointCampaigns.RemoveRange(db.PointCampaigns);
            db.RewardCatalogItems.RemoveRange(db.RewardCatalogItems);

            var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == TenantSeed.KBeautyTenantId);
            db.Entry(subscription).Property(nameof(TenantSubscription.PaidThroughUtc)).CurrentValue = DateTime.UtcNow.AddDays(30);

            await db.SaveChangesAsync();
        });
    }

    private async Task SeedWalletCustomerAsync()
    {
        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var customer = new Customer(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                "Auto Notify",
                "auto-notify@test.local",
                new DateTime(1990, 1, 1),
                now,
                phone: null);
            var card = new LoyaltyCard(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                customer.Id,
                Serial,
                now);

            db.Customers.Add(customer);
            db.LoyaltyCards.Add(card);
            db.DeviceRegistrations.Add(new DeviceRegistration(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                "device-auto-notify",
                "pass.com.kbeautymx.loyalty",
                Serial,
                PushToken,
                now));

            await db.SaveChangesAsync();
        });
    }

    private async Task MoveCampaignIntoCurrentWindowAsync(Guid campaignId)
    {
        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var campaign = await db.PointCampaigns.SingleAsync(c => c.Id == campaignId);
            campaign.Update(
                campaign.Name,
                campaign.Description,
                campaign.Multiplier,
                campaign.MinimumPurchaseAmount,
                campaign.LevelEligibility,
                DateTime.UtcNow.AddMinutes(-10),
                DateTime.UtcNow.AddHours(2),
                DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
    }

    private async Task MoveRewardIntoCurrentWindowAsync(Guid rewardId)
    {
        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var reward = await db.RewardCatalogItems.SingleAsync(r => r.Id == rewardId);
            reward.Update(
                reward.Name,
                reward.Description,
                reward.PointsCost,
                reward.MinLevel,
                true,
                DateTime.UtcNow.AddMinutes(-10),
                DateTime.UtcNow.AddHours(2));
            await db.SaveChangesAsync();
        });
    }

    private async Task<int> CountNotificationsAsync(NotificationType type) =>
        await WithTenantAsync(sp => sp.GetRequiredService<AppDbContext>()
            .LoyaltyNotifications
            .CountAsync(n => n.Type == type));

    private async Task<int> CountCampaignNotificationsAsync(Guid campaignId)
    {
        var prefix = $"point-campaign-started:{campaignId:N}:";
        return await WithTenantAsync(sp => sp.GetRequiredService<AppDbContext>()
            .LoyaltyNotifications
            .CountAsync(n => n.Type == NotificationType.PointCampaignStarted
                          && n.CorrelationId != null
                          && n.CorrelationId.StartsWith(prefix)));
    }

    private async Task<int> CountMonthlyProductNotificationsAsync(Guid rewardId)
    {
        var prefix = $"monthly-product-started:{rewardId:N}:";
        return await WithTenantAsync(sp => sp.GetRequiredService<AppDbContext>()
            .LoyaltyNotifications
            .CountAsync(n => n.Type == NotificationType.MonthlyProductStarted
                          && n.CorrelationId != null
                          && n.CorrelationId.StartsWith(prefix)));
    }

    private async Task<bool> CampaignExistsAsync(Guid campaignId) =>
        await WithTenantAsync(sp => sp.GetRequiredService<AppDbContext>()
            .PointCampaigns
            .AnyAsync(c => c.Id == campaignId));

    private async Task<bool> RewardExistsAsync(Guid rewardId) =>
        await WithTenantAsync(sp => sp.GetRequiredService<AppDbContext>()
            .RewardCatalogItems
            .AnyAsync(r => r.Id == rewardId));

    private async Task<int> CountDeliveriesAsync(NotificationType type, NotificationDeliveryStatus status) =>
        await WithTenantAsync(sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return (
                from delivery in db.NotificationDeliveries
                join notification in db.LoyaltyNotifications
                    on new { delivery.TenantId, Id = delivery.LoyaltyNotificationId }
                    equals new { notification.TenantId, notification.Id }
                where notification.Type == type && delivery.Status == status
                select delivery).CountAsync();
        });

    private async Task<int> CountDeliveriesTotalAsync(NotificationType type) =>
        await WithTenantAsync(sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return (
                from delivery in db.NotificationDeliveries
                join notification in db.LoyaltyNotifications
                    on new { delivery.TenantId, Id = delivery.LoyaltyNotificationId }
                    equals new { notification.TenantId, notification.Id }
                where notification.Type == type
                select delivery).CountAsync();
        });

    private async Task<NotificationDelivery> GetSingleDeliveryAsync(NotificationType type) =>
        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return await (
                from delivery in db.NotificationDeliveries
                join notification in db.LoyaltyNotifications
                    on new { delivery.TenantId, Id = delivery.LoyaltyNotificationId }
                    equals new { notification.TenantId, notification.Id }
                where notification.Type == type
                select delivery).SingleAsync();
        });

    private async Task AgeSingleDeliveryAsync(NotificationType type, DateTime completedAtUtc) =>
        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var delivery = await (
                from row in db.NotificationDeliveries
                join notification in db.LoyaltyNotifications
                    on new { row.TenantId, Id = row.LoyaltyNotificationId }
                    equals new { notification.TenantId, notification.Id }
                where notification.Type == type
                select row).SingleAsync();
            db.Entry(delivery).Property(nameof(NotificationDelivery.CompletedAt)).CurrentValue = completedAtUtc;
            await db.SaveChangesAsync();
        });

    private async Task<Guid> SeedStuckProcessingNotificationAsync() =>
        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var card = await db.LoyaltyCards.SingleAsync(c => c.SerialNumber == Serial);
            var notification = new LoyaltyNotification(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                card.CustomerId,
                card.Id,
                NotificationType.Custom,
                "Mensaje",
                "Mensaje atorado",
                DateTime.UtcNow.AddMinutes(-30),
                null,
                DateTime.UtcNow.AddHours(1),
                $"stuck-test:{Guid.NewGuid():N}",
                "test",
                null);
            var delivery = new NotificationDelivery(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                notification.Id,
                NotificationChannel.AppleWallet,
                DateTime.UtcNow.AddMinutes(-30));
            notification.AddDelivery(delivery);
            notification.MarkProcessing(DateTime.UtcNow.AddMinutes(-20));
            delivery.MarkProcessing(DateTime.UtcNow.AddMinutes(-20));
            db.LoyaltyNotifications.Add(notification);
            await db.SaveChangesAsync();
            return notification.Id;
        });

    private async Task<WalletNotificationContext> GetWalletNotificationContextAsync() =>
        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var card = await db.LoyaltyCards.SingleAsync(c => c.SerialNumber == Serial);
            return await sp.GetRequiredService<IWalletNotificationReadService>().GetActiveContextAsync(card.Id);
        });

    private async Task<T> WithTenantAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        return await action(scope.ServiceProvider);
    }

    private async Task WithTenantAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        await action(scope.ServiceProvider);
    }

    private static HttpRequestMessage CreateSignedRequest(HttpMethod method, string path, object? body)
    {
        const string tenantSlug = "kbeauty";
        const string operatorId = "automatic-wallet-notification-test";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var bodyBytes = body is null
            ? []
            : JsonSerializer.SerializeToUtf8Bytes(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var signature = AdminApiSignature.CreateSignature(
            SharedSecret,
            method.Method,
            path,
            timestamp,
            tenantSlug,
            operatorId,
            bodyBytes);

        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(bodyBytes);
            request.Content.Headers.ContentType = new("application/json");
        }

        request.Headers.Add(AdminApiSignature.TenantSlugHeader, tenantSlug);
        request.Headers.Add(AdminApiSignature.OperatorHeader, operatorId);
        request.Headers.Add(AdminApiSignature.TimestampHeader, timestamp);
        request.Headers.Add(AdminApiSignature.SignatureHeader, signature);
        return request;
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoyaltyCloud.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontro LoyaltyCloud.sln.");
    }
}
