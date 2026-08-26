using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyCloud.Application.Config.Queries.GetProgramConfig;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Security;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class AdminApiPointsFlowTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private const string SharedSecret = "test-admin-api-shared-secret-with-enough-length";
    private static readonly Guid BellaTenantId = Guid.Parse("c2000000-0000-0000-0000-000000000001");
    private const string BellaTenantSlug = "bella-api";
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminApiPointsFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureDatabaseCreatedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "WalletProductionUpdate")]
    public async Task Add_points_api_rejects_unsigned_admin_request()
    {
        using var response = await _client.PostAsJsonAsync("/api/points", new
        {
            serialNumber = "KB-NOSIGN",
            purchaseAmount = 100m
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "WalletProductionUpdate")]
    public async Task Signed_admin_points_request_runs_in_api_and_attempts_wallet_push()
    {
        var serial = "KB-APNAPI1";
        await SeedCardWithDeviceAsync(serial);
        var initialApnCount = _factory.Apn.Calls.Count;

        using var request = CreateSignedAddPointsRequest(serial, 100m);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AddPointsResponse>();
        Assert.NotNull(result);
        Assert.Equal(10, result!.PointsAdded);
        Assert.True(_factory.Apn.Calls.Count > initialApnCount);
        Assert.Contains(_factory.Apn.Calls, call => call.Token == "push-token-api-flow");

        using var scope = _factory.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<IMutableTenantContext>();
        tenantContext.SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = await db.LoyaltyNotifications
            .AsNoTracking()
            .Where(n => n.TenantId == TenantSeed.KBeautyTenantId
                     && n.Type == NotificationType.PointsAdded)
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(notification);
        Assert.Equal(NotificationStatus.Delivered, notification!.Status);
        Assert.Contains("\"pointsAdded\":10", notification.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("\"newTotal\":10", notification.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "AdminInteractiveTenantContext")]
    public async Task Signed_admin_custom_notification_campaigns_request_sets_tenant_context()
    {
        await EnsureTenantOperationalAsync();

        using var request = CreateSignedRequest(
            HttpMethod.Get,
            "/api/custom-notification-campaigns?take=100",
            body: null);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var campaigns = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(campaigns);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public async Task Redemption_catalog_rejects_unsigned_admin_request()
    {
        using var response = await _client.GetAsync("/api/redemptions/catalog/KB-NOSIGN");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public async Task Signed_admin_redemption_catalog_request_reaches_api()
    {
        await EnsureTenantOperationalAsync();

        using var request = CreateSignedRequest(
            HttpMethod.Get,
            "/api/redemptions/catalog/KB-MISSING",
            body: null);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public async Task Signed_admin_config_request_resolves_tenant_and_returns_only_that_tenant_config()
    {
        await EnsureTenantOperationalAsync();
        await EnsureBellaTenantWithConfigAsync();

        using var kbeautyRequest = CreateSignedRequest(
            HttpMethod.Get,
            "/api/config",
            body: null,
            tenantSlug: TenantSeed.KBeautySlug);
        using var kbeautyResponse = await _client.SendAsync(kbeautyRequest);

        Assert.Equal(HttpStatusCode.OK, kbeautyResponse.StatusCode);
        var kbeautyConfig = await kbeautyResponse.Content.ReadFromJsonAsync<List<ConfigDto>>();
        Assert.NotNull(kbeautyConfig);
        Assert.Contains(kbeautyConfig!, entry =>
            entry.Key == LoyaltyConstants.ConfigKeys.PointsPerPesoUnit
            && entry.Value == LoyaltyConstants.Defaults.PointsPerPesoUnit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.DoesNotContain(kbeautyConfig!, entry =>
            entry.Key == LoyaltyConstants.ConfigKeys.PointsPerPesoUnit
            && entry.Value == "42");

        using var bellaRequest = CreateSignedRequest(
            HttpMethod.Get,
            "/api/config",
            body: null,
            tenantSlug: BellaTenantSlug);
        using var bellaResponse = await _client.SendAsync(bellaRequest);

        Assert.Equal(HttpStatusCode.OK, bellaResponse.StatusCode);
        var bellaConfig = await bellaResponse.Content.ReadFromJsonAsync<List<ConfigDto>>();
        Assert.NotNull(bellaConfig);
        Assert.Contains(bellaConfig!, entry =>
            entry.Key == LoyaltyConstants.ConfigKeys.PointsPerPesoUnit
            && entry.Value == "42");
        Assert.DoesNotContain(bellaConfig!, entry =>
            entry.Key == LoyaltyConstants.ConfigKeys.PointsPerPesoUnit
            && entry.Value == LoyaltyConstants.Defaults.PointsPerPesoUnit.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var customerRequest = CreateSignedRequest(
            HttpMethod.Get,
            "/api/customers/KB-MISSING",
            body: null,
            tenantSlug: BellaTenantSlug);
        using var customerResponse = await _client.SendAsync(customerRequest);
        Assert.Equal(HttpStatusCode.NotFound, customerResponse.StatusCode);

        using var catalogRequest = CreateSignedRequest(
            HttpMethod.Get,
            "/api/redemptions/catalog/KB-MISSING",
            body: null,
            tenantSlug: BellaTenantSlug);
        using var catalogResponse = await _client.SendAsync(catalogRequest);
        Assert.Equal(HttpStatusCode.NotFound, catalogResponse.StatusCode);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    [Trait("Category", "WalletProductionUpdate")]
    public async Task Signed_admin_wallet_branding_request_touches_only_tenant_cards_and_attempts_apple_wallet_push()
    {
        _factory.Apn.NextResult = null;
        var kbeautySerial = "KB-BRAND1";
        var bellaSerial = "KB-BELLA1";
        var before = DateTime.UtcNow.AddHours(-2);

        await SeedCardWithDeviceAsync(kbeautySerial, "kbeauty-branding-device", "kbeauty-branding-token", before);
        await EnsureBellaTenantWithConfigAsync();
        await SeedBellaCardWithDeviceAsync(bellaSerial, "bella-branding-device", "bella-branding-token", before);
        var initialApnCount = _factory.Apn.Calls.Count;

        using var request = CreateSignedRequest(
            HttpMethod.Put,
            "/api/config/wallet-branding",
            new { walletBackgroundColor = "#1c1c1c" },
            tenantSlug: TenantSeed.KBeautySlug);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var branding = await response.Content.ReadFromJsonAsync<TenantBrandingInfo>();
        Assert.NotNull(branding);
        Assert.Equal(TenantSeed.KBeautyTenantId, branding!.TenantId);
        Assert.Equal("#1C1C1C", branding.WalletBackgroundColor);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var kbeautyCard = await db.LoyaltyCards
            .IgnoreQueryFilters()
            .SingleAsync(c => c.SerialNumber == kbeautySerial);
        var bellaCard = await db.LoyaltyCards
            .IgnoreQueryFilters()
            .SingleAsync(c => c.SerialNumber == bellaSerial);

        Assert.True(kbeautyCard.LastActivityAt > before);
        Assert.Equal(before, bellaCard.LastActivityAt);
        Assert.Contains(_factory.Apn.Calls.Skip(initialApnCount), call =>
            call.Token == "kbeauty-branding-token"
            && call.Reason == PassUpdateReason.BrandingUpdated);
        Assert.DoesNotContain(_factory.Apn.Calls.Skip(initialApnCount), call =>
            call.Token == "bella-branding-token");

        var since = new DateTimeOffset(before).ToUnixTimeSeconds();
        using var registrations = await _client.GetAsync($"/v1/devices/kbeauty-branding-device/registrations/pass.com.kbeautymx.loyalty?passesUpdatedSince={since}");
        Assert.Equal(HttpStatusCode.OK, registrations.StatusCode);
        var payload = await registrations.Content.ReadAsStringAsync();
        Assert.Contains(kbeautySerial, payload, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    [Trait("Category", "WalletProductionUpdate")]
    public async Task Signed_admin_wallet_branding_request_keeps_branding_when_apns_is_rejected()
    {
        _factory.Apn.NextResult = ApnPushResult.Permanent(400, "BadDeviceToken");
        var serial = "KB-BRAND2";
        await SeedCardWithDeviceAsync(serial, "kbeauty-branding-device-2", "kbeauty-branding-token-2", DateTime.UtcNow.AddHours(-2));

        using var request = CreateSignedRequest(
            HttpMethod.Put,
            "/api/config/wallet-branding",
            new { walletBackgroundColor = "#222222" },
            tenantSlug: TenantSeed.KBeautySlug);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<IMutableTenantContext>();
        tenantContext.SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var branding = await db.TenantBrandings.SingleAsync(b => b.TenantId == TenantSeed.KBeautyTenantId);
        Assert.Equal("#222222", branding.WalletBackgroundColor);

        _factory.Apn.NextResult = null;
    }

    private async Task EnsureTenantOperationalAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<IMutableTenantContext>();
        tenantContext.SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == TenantSeed.KBeautyTenantId);
        db.Entry(subscription).Property(nameof(TenantSubscription.PaidThroughUtc)).CurrentValue = DateTime.UtcNow.AddDays(30);
        await db.SaveChangesAsync();
    }

    private Task SeedCardWithDeviceAsync(string serial) =>
        SeedCardWithDeviceAsync(serial, "device-api-flow", "push-token-api-flow", DateTime.UtcNow);

    private async Task SeedCardWithDeviceAsync(
        string serial,
        string deviceIdentifier,
        string pushToken,
        DateTime now)
    {
        using var scope = _factory.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<IMutableTenantContext>();
        tenantContext.SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == TenantSeed.KBeautyTenantId);
        db.Entry(subscription).Property(nameof(TenantSubscription.PaidThroughUtc)).CurrentValue = DateTime.UtcNow.AddDays(30);

        if (await db.LoyaltyCards.AnyAsync(c => c.SerialNumber == serial))
        {
            await db.SaveChangesAsync();
            return;
        }

        var customer = new Customer(
            Guid.NewGuid(),
            TenantSeed.KBeautyTenantId,
            "Wallet API Customer",
            $"wallet-api-{Guid.NewGuid():N}@test.local",
            new DateTime(1990, 1, 1),
            now,
            "6460000000");
        var card = new LoyaltyCard(
            Guid.NewGuid(),
            TenantSeed.KBeautyTenantId,
            customer.Id,
            serial,
            now);

        db.Customers.Add(customer);
        db.LoyaltyCards.Add(card);
        db.DeviceRegistrations.Add(new DeviceRegistration(
            Guid.NewGuid(),
            TenantSeed.KBeautyTenantId,
            deviceIdentifier,
            "pass.com.kbeautymx.loyalty",
            serial,
            pushToken,
            now));
        await db.SaveChangesAsync();
    }

    private async Task SeedBellaCardWithDeviceAsync(
        string serial,
        string deviceIdentifier,
        string pushToken,
        DateTime now)
    {
        using var scope = _factory.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<IMutableTenantContext>();
        tenantContext.SetTenant(BellaTenantId, BellaTenantSlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.LoyaltyCards.AnyAsync(c => c.SerialNumber == serial))
            return;

        var customer = new Customer(
            Guid.NewGuid(),
            BellaTenantId,
            "Bella Wallet Customer",
            $"bella-wallet-{Guid.NewGuid():N}@test.local",
            new DateTime(1991, 1, 1),
            now,
            "6460000001");
        var card = new LoyaltyCard(
            Guid.NewGuid(),
            BellaTenantId,
            customer.Id,
            serial,
            now);

        db.Customers.Add(customer);
        db.LoyaltyCards.Add(card);
        db.DeviceRegistrations.Add(new DeviceRegistration(
            Guid.NewGuid(),
            BellaTenantId,
            deviceIdentifier,
            "pass.com.kbeautymx.loyalty",
            serial,
            pushToken,
            now));
        await db.SaveChangesAsync();
    }

    private async Task EnsureBellaTenantWithConfigAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<IMutableTenantContext>();
        tenantContext.SetTenant(BellaTenantId, BellaTenantSlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == BellaTenantId))
        {
            var now = DateTime.UtcNow;
            db.Tenants.Add(new Tenant(
                BellaTenantId,
                BellaTenantSlug,
                "Bella API",
                "America/Tijuana",
                now));
            db.TenantBrandings.Add(new TenantBranding(
                BellaTenantId,
                primaryColor: "#2D2D2D",
                secondaryColor: "#7C3AED"));
            db.TenantSubscriptions.Add(new TenantSubscription(
                BellaTenantId,
                TenantSubscriptionStatus.Active,
                "internal",
                paidThroughUtc: now.AddDays(30)));
            db.ProgramConfigs.Add(new ProgramConfig(
                Guid.NewGuid(),
                BellaTenantId,
                LoyaltyConstants.ConfigKeys.PointsPerPesoUnit,
                "42",
                now,
                "Valor distintivo para prueba multi-tenant.",
                "test"));
            await db.SaveChangesAsync();
        }
    }

    private static HttpRequestMessage CreateSignedAddPointsRequest(string serial, decimal purchaseAmount) =>
        CreateSignedRequest(
            HttpMethod.Post,
            "/api/points",
            new { serialNumber = serial, purchaseAmount });

    private static HttpRequestMessage CreateSignedRequest(
        HttpMethod method,
        string path,
        object? body,
        string tenantSlug = TenantSeed.KBeautySlug)
    {
        const string operatorId = "admin-api-test";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var bodyBytes = body is null
            ? []
            : JsonSerializer.SerializeToUtf8Bytes(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
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
}
