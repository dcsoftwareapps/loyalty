using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Levels;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Security;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class MTLevel6TenantLoyaltyLevelAdminTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private const string SharedSecret = "test-admin-api-shared-secret-with-enough-length";
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MTLevel6TenantLoyaltyLevelAdminTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        await ResetKBeautyLevelsAsync();
        await EnsureTenantOperationalAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "MTLevel6")]
    public async Task Levels_api_lists_tenant_levels_in_sort_order()
    {
        using var request = CreateSignedRequest(HttpMethod.Get, "/api/levels", body: null);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var levels = await response.Content.ReadFromJsonAsync<List<TenantLoyaltyLevelAdminDto>>();
        Assert.NotNull(levels);
        Assert.Collection(
            levels!,
            level => Assert.Equal("Mist", level.Name),
            level => Assert.Equal("Glow", level.Name),
            level => Assert.Equal("Radiance", level.Name));
    }

    [Fact]
    [Trait("Category", "MTLevel6")]
    public async Task Levels_api_is_tenant_scoped_by_signed_tenant_header()
    {
        using var request = CreateSignedRequest(HttpMethod.Get, "/api/levels", body: null, tenantSlug: "missing-tenant");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [Trait("Category", "MTLevel6")]
    public async Task Levels_api_accepts_valid_three_to_five_levels(int count)
    {
        var current = await GetLevelsAsync();
        var items = new List<TenantLoyaltyLevelUpdateItemDto>
        {
            new(current.Single(level => level.Name == "Mist").Id, "Mist", 0),
            new(current.Single(level => level.Name == "Glow").Id, "Glow", 500),
            new(current.Single(level => level.Name == "Radiance").Id, "Radiance", 1000)
        };
        if (count >= 4)
            items.Add(new(null, "Platinum", 1500));
        if (count >= 5)
            items.Add(new(null, "Diamond", 2000));

        using var request = CreateSignedRequest(HttpMethod.Put, "/api/levels", new { levels = items });
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UpdateTenantLoyaltyLevelsResultDto>();
        Assert.NotNull(result);
        Assert.Equal(count, result!.Levels.Count);
        Assert.Equal(Enumerable.Range(1, count).ToArray(), result.Levels.Select(level => level.SortOrder).ToArray());
    }

    [Theory]
    [InlineData("less-than-three", "Mist:0|Glow:500")]
    [InlineData("more-than-five", "Mist:0|Glow:500|Radiance:1000|Platinum:1500|Diamond:2000|Elite:2500")]
    [InlineData("first-not-zero", "Mist:1|Glow:500|Radiance:1000")]
    [InlineData("non-ascending", "Mist:0|Glow:500|Radiance:500")]
    [InlineData("duplicate-name", "Mist:0|mist:500|Radiance:1000")]
    [InlineData("long-name", "Mist:0|NombreDemasiadoLargo21:500|Radiance:1000")]
    [Trait("Category", "MTLevel6")]
    public async Task Levels_api_rejects_invalid_level_configuration(string _, string spec)
    {
        var payload = new { levels = ParseNewLevels(spec) };

        using var request = CreateSignedRequest(HttpMethod.Put, "/api/levels", payload);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "MTLevel6")]
    public async Task Rename_same_sort_order_updates_operational_references_and_cards_without_level_changed_notification()
    {
        await SeedCardAsync("KB-MTL6-REN", "Glow", rollingPoints: 700, pushToken: "push-mtl6-ren");
        await SeedRewardCampaignAndAudienceAsync("Glow");
        var current = await GetLevelsAsync();
        var glowId = current.Single(level => level.Name == "Glow").Id;

        var payload = new
        {
            levels = new TenantLoyaltyLevelUpdateItemDto[]
            {
            new(current.Single(level => level.Name == "Mist").Id, "Mist", 0),
            new(glowId, "Bloom", 500),
            new(current.Single(level => level.Name == "Radiance").Id, "Radiance", 1000)
            }
        };

        using var request = CreateSignedRequest(HttpMethod.Put, "/api/levels", payload);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UpdateTenantLoyaltyLevelsResultDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result!.CardsChanged);
        Assert.Equal(0, result.CardsUpgraded);
        Assert.Equal(0, result.CardsDowngraded);

        await WithTenantAsync(async db =>
        {
            Assert.Equal("Bloom", await db.LoyaltyCards.Where(c => c.SerialNumber == "KB-MTL6-REN").Select(c => c.Level).SingleAsync());
            Assert.Equal("Bloom", await db.RewardCatalogItems.Select(r => r.MinLevel).SingleAsync());
            Assert.Equal("Bloom", await db.PointCampaigns.Select(c => c.LevelEligibility).SingleAsync());
            Assert.Equal("Bloom", await db.CustomNotificationCampaigns.Select(c => c.AudienceType).SingleAsync());
            Assert.False(await db.LoyaltyNotifications.AnyAsync(n => n.Type == NotificationType.LevelChanged));
        });

        Assert.Contains(_factory.Apn.Calls, call => call.Token == "push-mtl6-ren");
    }

    [Fact]
    [Trait("Category", "MTLevel6")]
    public async Task Threshold_update_recalculates_cards_and_only_upgrades_create_level_changed_notifications()
    {
        await SeedCardAsync("KB-MTL6-UP", "Mist", rollingPoints: 700, pushToken: "push-mtl6-up");
        await SeedCardAsync("KB-MTL6-DOWN", "Glow", rollingPoints: 450, pushToken: "push-mtl6-down");
        await SeedCardAsync("KB-MTL6-SAME", "Mist", rollingPoints: 100, pushToken: "push-mtl6-same");
        var initialApnCount = _factory.Apn.Calls.Count;
        var current = await GetLevelsAsync();

        var payload = new
        {
            levels = new TenantLoyaltyLevelUpdateItemDto[]
            {
            new(current.Single(level => level.Name == "Mist").Id, "Mist", 0),
            new(current.Single(level => level.Name == "Glow").Id, "Glow", 500),
            new(current.Single(level => level.Name == "Radiance").Id, "Radiance", 1000)
            }
        };

        using var request = CreateSignedRequest(HttpMethod.Put, "/api/levels", payload);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UpdateTenantLoyaltyLevelsResultDto>();
        Assert.NotNull(result);
        Assert.Equal(3, result!.CardsReviewed);
        Assert.Equal(2, result.CardsChanged);
        Assert.Equal(1, result.CardsUpgraded);
        Assert.Equal(1, result.CardsDowngraded);

        await WithTenantAsync(async db =>
        {
            Assert.Equal("Glow", await db.LoyaltyCards.Where(c => c.SerialNumber == "KB-MTL6-UP").Select(c => c.Level).SingleAsync());
            Assert.Equal("Mist", await db.LoyaltyCards.Where(c => c.SerialNumber == "KB-MTL6-DOWN").Select(c => c.Level).SingleAsync());
            Assert.Equal("Mist", await db.LoyaltyCards.Where(c => c.SerialNumber == "KB-MTL6-SAME").Select(c => c.Level).SingleAsync());
            Assert.Single(await db.LoyaltyNotifications.Where(n => n.Type == NotificationType.LevelChanged).ToListAsync());
        });

        var newCalls = _factory.Apn.Calls.Skip(initialApnCount).Select(call => call.Token).ToList();
        Assert.Contains("push-mtl6-up", newCalls);
        Assert.Contains("push-mtl6-down", newCalls);
        Assert.DoesNotContain("push-mtl6-same", newCalls);
    }

    [Fact]
    [Trait("Category", "MTLevel6")]
    public async Task Delete_referenced_level_is_rejected_with_clear_error()
    {
        await SeedRewardCampaignAndAudienceAsync("Glow");
        var current = await GetLevelsAsync();

        var payload = new
        {
            levels = new TenantLoyaltyLevelUpdateItemDto[]
            {
            new(current.Single(level => level.Name == "Mist").Id, "Mist", 0),
            new(current.Single(level => level.Name == "Radiance").Id, "Radiance", 1000),
            new(null, "Platinum", 1500)
            }
        };

        using var request = CreateSignedRequest(HttpMethod.Put, "/api/levels", payload);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = await response.Content.ReadAsStringAsync();
        Assert.Contains("recompensa", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("camp", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("audiencia", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "MTLevel6")]
    public async Task Delete_unreferenced_level_removes_it_and_recalculates_cards()
    {
        await EnsurePlatinumLevelAsync();
        await SeedCardAsync("KB-MTL6-DEL", "Platinum", rollingPoints: 1800, pushToken: "push-mtl6-del");
        var current = await GetLevelsAsync();

        var payload = new
        {
            levels = new TenantLoyaltyLevelUpdateItemDto[]
            {
            new(current.Single(level => level.Name == "Mist").Id, "Mist", 0),
            new(current.Single(level => level.Name == "Glow").Id, "Glow", 500),
            new(current.Single(level => level.Name == "Radiance").Id, "Radiance", 1000)
            }
        };

        using var request = CreateSignedRequest(HttpMethod.Put, "/api/levels", payload);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WithTenantAsync(async db =>
        {
            Assert.False(await db.TenantLoyaltyLevels.AnyAsync(level => level.Name == "Platinum"));
            Assert.Equal("Radiance", await db.LoyaltyCards.Where(c => c.SerialNumber == "KB-MTL6-DEL").Select(c => c.Level).SingleAsync());
        });
    }

    [Fact]
    [Trait("Category", "MTLevel6")]
    public async Task Adding_fourth_and_fifth_levels_recalculates_cards_against_new_sort_order()
    {
        await SeedCardAsync("KB-MTL6-ADD", "Radiance", rollingPoints: 2600, pushToken: "push-mtl6-add");
        var current = await GetLevelsAsync();

        var payload = new
        {
            levels = new TenantLoyaltyLevelUpdateItemDto[]
            {
                new(current.Single(level => level.Name == "Mist").Id, "Mist", 0),
                new(current.Single(level => level.Name == "Glow").Id, "Glow", 1000),
                new(null, "Platinum", 1500),
                new(current.Single(level => level.Name == "Radiance").Id, "Radiance", 2000),
                new(null, "Diamond", 2500)
            }
        };

        using var request = CreateSignedRequest(HttpMethod.Put, "/api/levels", payload);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WithTenantAsync(async db =>
        {
            Assert.Equal("Diamond", await db.LoyaltyCards.Where(c => c.SerialNumber == "KB-MTL6-ADD").Select(c => c.Level).SingleAsync());
            Assert.Equal(5, await db.TenantLoyaltyLevels.CountAsync());
        });
    }

    [Fact]
    [Trait("Category", "MTLevel6")]
    public void Admin_navigation_and_config_use_levels_page_without_legacy_threshold_entries()
    {
        var root = GetRepositoryRoot();
        var menu = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Components", "Layout", "MainLayout.razor"));
        var levels = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "Levels.razor"));
        var config = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "Config.razor"));

        Assert.Contains("href=\"/levels\"", menu);
        Assert.Contains("@page \"/levels\"", levels);
        Assert.Contains("PutAsJsonAsync<UpdateTenantLoyaltyLevelsRequest, UpdateTenantLoyaltyLevelsResultDto>", levels);
        Assert.Contains("LevelMistMin", config);
        Assert.Contains("LevelGlowMin", config);
        Assert.Contains("LevelRadianceMin", config);
        Assert.Contains("RadianceRequalificationPoints", config);
    }

    private async Task<IReadOnlyList<TenantLoyaltyLevelAdminDto>> GetLevelsAsync()
    {
        using var request = CreateSignedRequest(HttpMethod.Get, "/api/levels", body: null);
        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<TenantLoyaltyLevelAdminDto>>())!;
    }

    private static IReadOnlyList<TenantLoyaltyLevelUpdateItemDto> ParseNewLevels(string spec) =>
        spec.Split('|')
            .Select(item =>
            {
                var parts = item.Split(':');
                return new TenantLoyaltyLevelUpdateItemDto(null, parts[0], int.Parse(parts[1]));
            })
            .ToList();

    private async Task ResetKBeautyLevelsAsync()
    {
        await WithTenantAsync(async db =>
        {
            db.TenantLoyaltyLevels.RemoveRange(db.TenantLoyaltyLevels);
            db.RewardCatalogItems.RemoveRange(db.RewardCatalogItems);
            db.PointCampaigns.RemoveRange(db.PointCampaigns);
            db.CustomNotificationCampaigns.RemoveRange(db.CustomNotificationCampaigns);
            db.LoyaltyNotifications.RemoveRange(db.LoyaltyNotifications);
            db.DeviceRegistrations.RemoveRange(db.DeviceRegistrations.Where(d => d.SerialNumber.StartsWith("KB-MTL6")));
            db.PointTransactions.RemoveRange(db.PointTransactions.Where(t => t.Description.StartsWith("MT6")));
            db.LoyaltyCards.RemoveRange(db.LoyaltyCards.Where(c => c.SerialNumber.StartsWith("KB-MTL6")));
            db.Customers.RemoveRange(db.Customers.Where(c => c.Email.Contains("@mt6.test")));

            var now = DateTime.UtcNow;
            db.TenantLoyaltyLevels.AddRange(
                new TenantLoyaltyLevel(Guid.NewGuid(), TenantSeed.KBeautyTenantId, "Mist", 0, 1, now),
                new TenantLoyaltyLevel(Guid.NewGuid(), TenantSeed.KBeautyTenantId, "Glow", 1000, 2, now),
                new TenantLoyaltyLevel(Guid.NewGuid(), TenantSeed.KBeautyTenantId, "Radiance", 2000, 3, now));
            await db.SaveChangesAsync();
        });
        _factory.Apn.Calls.Clear();
    }

    private async Task EnsureTenantOperationalAsync()
    {
        await WithTenantAsync(async db =>
        {
            var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == TenantSeed.KBeautyTenantId);
            db.Entry(subscription).Property(nameof(TenantSubscription.PaidThroughUtc)).CurrentValue = DateTime.UtcNow.AddDays(30);
            await db.SaveChangesAsync();
        });
    }

    private async Task EnsurePlatinumLevelAsync()
    {
        await WithTenantAsync(async db =>
        {
            if (await db.TenantLoyaltyLevels.AnyAsync(level => level.Name == "Platinum"))
                return;
            db.TenantLoyaltyLevels.Add(new TenantLoyaltyLevel(Guid.NewGuid(), TenantSeed.KBeautyTenantId, "Platinum", 1500, 4, DateTime.UtcNow));
            await db.SaveChangesAsync();
        });
    }

    private async Task SeedRewardCampaignAndAudienceAsync(string levelName)
    {
        await WithTenantAsync(async db =>
        {
            db.RewardCatalogItems.Add(new RewardCatalogItem(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                $"Reward {Guid.NewGuid():N}",
                "Reward for MT-Level-6.",
                100,
                levelName));
            db.PointCampaigns.Add(new PointCampaign(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                $"Campaign {Guid.NewGuid():N}",
                "Campaign for MT-Level-6.",
                2,
                null,
                levelName,
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow));
            db.CustomNotificationCampaigns.Add(new CustomNotificationCampaign(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                $"Custom {Guid.NewGuid():N}",
                "Novedad",
                "Mensaje",
                "Mensaje largo",
                levelName,
                null,
                null,
                null,
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow));
            await db.SaveChangesAsync();
        });
    }

    private async Task SeedCardAsync(string serial, string level, int rollingPoints, string pushToken)
    {
        await WithTenantAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var customer = new Customer(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                $"MT6 {serial}",
                $"{serial.ToLowerInvariant()}@mt6.test",
                new DateTime(1990, 1, 1),
                now,
                phone: null);
            var card = new LoyaltyCard(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                customer.Id,
                serial,
                now);
            card.ApplyConfiguredLevelSilently(new LoyaltyCloud.Domain.ValueObjects.MemberLevel(Guid.NewGuid(), level, 0, int.MaxValue, level == "Mist" ? 1 : level == "Glow" ? 2 : level == "Radiance" ? 3 : 4), new FixedClock(now), updateLevelAchievedAt: true);

            db.Customers.Add(customer);
            db.LoyaltyCards.Add(card);
            db.PointTransactions.Add(new PointTransaction(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                card.Id,
                rollingPoints,
                TransactionType.Purchase,
                $"MT6 rolling {serial}",
                now.AddDays(-1),
                purchaseAmount: rollingPoints * 10m,
                basePoints: rollingPoints,
                appliedMultiplier: 1m));
            db.DeviceRegistrations.Add(new DeviceRegistration(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                $"device-{serial}",
                "pass.com.kbeautymx.loyalty",
                serial,
                pushToken,
                now));
            await db.SaveChangesAsync();
        });
    }

    private async Task WithTenantAsync(Func<AppDbContext, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static HttpRequestMessage CreateSignedRequest(
        HttpMethod method,
        string path,
        object? body,
        string tenantSlug = "kbeauty")
    {
        const string operatorId = "mt-level-6-test";
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

    private sealed class FixedClock(DateTime now) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = now;
        public DateTime Today => UtcNow.Date;
    }
}
