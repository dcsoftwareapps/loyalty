using MediatR;
using Xunit;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.Custom.Commands.ProcessCustomNotificationCampaign;
using LoyaltyCloud.Application.Notifications.Custom.Queries.PreviewCustomNotificationAudience;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoyaltyCloud.Tests.Integration;

public sealed class GoogleWalletCampaignNotificationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private static readonly Guid TenantBId = Guid.Parse("c3000000-0000-0000-0000-000000000001");
    private const string TenantBSlug = "wallet-tenant-b";
    private const string AppleSerial = "GW-NOTIFY-APPLE";
    private const string GoogleSerial = "GW-NOTIFY-GOOGLE";
    private const string NoWalletSerial = "GW-NOTIFY-NONE";
    private const string InvalidSerial = "GW-NOTIFY-INVALID";
    private const string TenantBSerial = "GW-NOTIFY-TENANT-B";

    private readonly CustomWebApplicationFactory _factory;

    public GoogleWalletCampaignNotificationTests(CustomWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        _factory.GoogleWallet.Messages.Clear();
        _factory.GoogleWallet.FailingObjectId = null;
        _factory.Apn.Calls.Clear();
        await SeedPrimaryTenantAsync();
        await SeedTenantBAsync();
    }

    public Task DisposeAsync()
    {
        _factory.GoogleWallet.FailingObjectId = null;
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "GoogleWalletNotifications")]
    public async Task Audience_counts_Apple_Google_and_no_wallet_without_duplicates()
    {
        var result = await WithTenantAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, sp =>
            sp.GetRequiredService<ISender>().Send(new PreviewCustomNotificationAudienceQuery(
                CustomNotificationCampaign.AudienceAllWalletUsers, null, null, 100)));

        Assert.True(result.IsSuccess, result.Error);
        var preview = result.Value;
        var sample = preview.SampleRecipients.Where(r =>
            r.SerialNumber is AppleSerial or GoogleSerial or NoWalletSerial).ToList();

        Assert.Equal(2, sample.Count);
        Assert.Contains(sample, r => r.SerialNumber == AppleSerial && r.DeviceRegistrationCount == 1 && r.GoogleWalletCount == 0);
        Assert.Contains(sample, r => r.SerialNumber == GoogleSerial && r.DeviceRegistrationCount == 0 && r.GoogleWalletCount == 1);
        Assert.DoesNotContain(sample, r => r.SerialNumber == NoWalletSerial);
        Assert.True(preview.AppleRecipients >= 1);
        Assert.True(preview.GoogleRecipients >= 1);
        Assert.True(preview.ExcludedWithoutDeviceRegistration >= 1);
    }

    [Fact]
    [Trait("Category", "GoogleWalletNotifications")]
    public async Task Mixed_campaign_routes_each_recipient_to_its_wallet_provider()
    {
        var campaignId = Guid.NewGuid();
        await WithTenantAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.CustomNotificationCampaigns.Add(new CustomNotificationCampaign(
                campaignId,
                TenantSeed.KBeautyTenantId,
                "Mixed wallet campaign",
                "NOVEDAD",
                "Brillitos hoy",
                "Hoy tenemos brillitos de regalo al visitar la tienda.",
                CustomNotificationCampaign.AudienceAllWalletUsers,
                null,
                null,
                null,
                DateTime.UtcNow.AddDays(2),
                DateTime.UtcNow));
            await db.SaveChangesAsync();
        });

        var result = await WithTenantAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, sp =>
            sp.GetRequiredService<ISender>().Send(new ProcessCustomNotificationCampaignCommand(campaignId)));

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(result.Value.NotificationsSucceeded >= 2);
        Assert.Contains(_factory.GoogleWallet.Messages, m =>
            m.ObjectId == ObjectId(TenantSeed.KBeautyTenantId, GoogleSerial) &&
            m.Header == "Brillitos hoy" &&
            m.Body == "Hoy tenemos brillitos de regalo al visitar la tienda.");
        Assert.Contains(_factory.Apn.Calls, call => call.Token == $"push-{AppleSerial}");

        await WithTenantAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var deliveries = await db.NotificationDeliveries
                .Where(d => db.LoyaltyNotifications.Any(n => n.Id == d.LoyaltyNotificationId && n.CustomNotificationCampaignId == campaignId))
                .ToListAsync();
            Assert.Contains(deliveries, d => d.Channel == NotificationChannel.AppleWallet && d.Status == NotificationDeliveryStatus.Succeeded);
            Assert.Contains(deliveries, d => d.Channel == NotificationChannel.GoogleWallet && d.Status == NotificationDeliveryStatus.Succeeded);
        });
    }

    [Fact]
    [Trait("Category", "GoogleWalletNotifications")]
    public async Task Invalid_Google_object_is_failed_and_not_reported_as_success()
    {
        var objectId = ObjectId(TenantSeed.KBeautyTenantId, InvalidSerial);
        _factory.GoogleWallet.FailingObjectId = objectId;

        var dto = await SendGoogleAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, InvalidSerial, "invalid-google");

        Assert.Equal(NotificationStatus.Failed, dto.Status);
        var delivery = Assert.Single(dto.Deliveries);
        Assert.Equal(NotificationChannel.GoogleWallet, delivery.Channel);
        Assert.Equal(NotificationDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(0, delivery.PushesAccepted);
        Assert.DoesNotContain(_factory.GoogleWallet.Messages, m => m.ObjectId == objectId);
    }

    [Fact]
    [Trait("Category", "GoogleWalletNotifications")]
    public async Task Concurrent_tenants_send_only_to_their_own_Google_objects()
    {
        var tenantASerial = GoogleSerial;
        var tenantAObject = ObjectId(TenantSeed.KBeautyTenantId, tenantASerial);
        var tenantBObject = ObjectId(TenantBId, TenantBSerial);

        await Task.WhenAll(
            SendGoogleAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, tenantASerial, "tenant-a"),
            SendGoogleAsync(TenantBId, TenantBSlug, TenantBSerial, "tenant-b"));

        var relevant = _factory.GoogleWallet.Messages
            .Where(m => m.MessageId.Contains("notification-", StringComparison.Ordinal))
            .Select(m => m.ObjectId)
            .ToList();
        Assert.Contains(tenantAObject, relevant);
        Assert.Contains(tenantBObject, relevant);
        Assert.DoesNotContain(relevant, id => id == ObjectId(TenantSeed.KBeautyTenantId, TenantBSerial));
        Assert.DoesNotContain(relevant, id => id == ObjectId(TenantBId, tenantASerial));
    }

    private async Task<LoyaltyCloud.Application.Notifications.NotificationDto> SendGoogleAsync(
        Guid tenantId,
        string tenantSlug,
        string serial,
        string correlationSuffix) =>
        await WithTenantAsync(tenantId, tenantSlug, sp =>
            sp.GetRequiredService<ILoyaltyNotificationService>().CreateAsync(new CreateLoyaltyNotificationRequest(
                serial,
                NotificationType.Custom,
                "NOVEDAD",
                "A entrenar!",
                null,
                DateTime.UtcNow.AddDays(2),
                [NotificationChannel.GoogleWallet],
                $"google-wallet-test:{correlationSuffix}:{Guid.NewGuid():N}",
                "test",
                null,
                true,
                ShortMessage: "A entrenar!",
                LongMessage: "A entrenar!")));

    private async Task SeedPrimaryTenantAsync()
    {
        await WithTenantAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            await RemoveTestRowsAsync(db);
            await AddCardAsync(db, TenantSeed.KBeautyTenantId, AppleSerial, apple: true, google: false);
            await AddCardAsync(db, TenantSeed.KBeautyTenantId, GoogleSerial, apple: false, google: true);
            await AddCardAsync(db, TenantSeed.KBeautyTenantId, NoWalletSerial, apple: false, google: false);
            await AddCardAsync(db, TenantSeed.KBeautyTenantId, InvalidSerial, apple: false, google: true);
            await db.SaveChangesAsync();
        });
    }

    private async Task SeedTenantBAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == TenantBId))
            {
                db.Tenants.Add(new Tenant(TenantBId, TenantBSlug, "Wallet Tenant B", "America/Tijuana", DateTime.UtcNow));
                await db.SaveChangesAsync();
            }
        }

        await WithTenantAsync(TenantBId, TenantBSlug, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            await RemoveTestRowsAsync(db);
            await AddCardAsync(db, TenantBId, TenantBSerial, apple: false, google: true);
            await db.SaveChangesAsync();
        });
    }

    private static async Task AddCardAsync(AppDbContext db, Guid tenantId, string serial, bool apple, bool google)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer(Guid.NewGuid(), tenantId, $"Test {serial}", $"{serial.ToLowerInvariant()}@example.test", Customer.BirthdayNotCaptured, now, null);
        var card = new LoyaltyCard(Guid.NewGuid(), tenantId, customer.Id, serial, now);
        db.Customers.Add(customer);
        db.LoyaltyCards.Add(card);

        if (apple)
        {
            db.DeviceRegistrations.Add(new DeviceRegistration(Guid.NewGuid(), tenantId, $"device-{serial}", "pass.test.loyalty", serial, $"push-{serial}", now));
        }

        if (google)
        {
            var wallet = new MemberDigitalWallet(Guid.NewGuid(), tenantId, customer.Id, card.Id, DigitalWalletProvider.Google, ClassId(tenantId), ObjectId(tenantId, serial), now);
            wallet.MarkSynchronized(now);
            db.MemberDigitalWallets.Add(wallet);
        }

        await Task.CompletedTask;
    }

    private static async Task RemoveTestRowsAsync(AppDbContext db)
    {
        var cards = await db.LoyaltyCards.Where(c => c.SerialNumber.StartsWith("GW-NOTIFY-")).ToListAsync();
        var cardIds = cards.Select(c => c.Id).ToArray();
        var customerIds = cards.Select(c => c.CustomerId).ToArray();
        db.NotificationDeliveries.RemoveRange(db.NotificationDeliveries.Where(d => db.LoyaltyNotifications.Any(n => n.Id == d.LoyaltyNotificationId && cardIds.Contains(n.LoyaltyCardId))));
        db.LoyaltyNotifications.RemoveRange(db.LoyaltyNotifications.Where(n => cardIds.Contains(n.LoyaltyCardId)));
        db.MemberDigitalWallets.RemoveRange(db.MemberDigitalWallets.Where(w => cardIds.Contains(w.LoyaltyCardId)));
        db.DeviceRegistrations.RemoveRange(db.DeviceRegistrations.Where(d => d.SerialNumber.StartsWith("GW-NOTIFY-")));
        db.LoyaltyCards.RemoveRange(cards);
        db.Customers.RemoveRange(db.Customers.Where(c => customerIds.Contains(c.Id)));
        db.CustomNotificationCampaigns.RemoveRange(db.CustomNotificationCampaigns.Where(c => c.Name == "Mixed wallet campaign"));
        await db.SaveChangesAsync();
    }

    private static string ClassId(Guid tenantId) => $"issuer-test.loyalty_{tenantId:N}";
    private static string ObjectId(Guid tenantId, string serial) => $"issuer-test.member_{tenantId:N}_{serial.ToLowerInvariant()}";

    private async Task<T> WithTenantAsync<T>(Guid tenantId, string slug, Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
        return await action(scope.ServiceProvider);
    }

    private async Task WithTenantAsync(Guid tenantId, string slug, Func<IServiceProvider, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
        await action(scope.ServiceProvider);
    }
}
