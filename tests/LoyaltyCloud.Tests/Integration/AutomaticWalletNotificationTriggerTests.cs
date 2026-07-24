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
    private const string Serial = "KB-AUTO-NOTIFY";
    private const string PushToken = "push-auto-notify";

    private readonly CustomWebApplicationFactory _factory;

    public AutomaticWalletNotificationTriggerTests(CustomWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        _factory.Apn.Calls.Clear();
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
}
