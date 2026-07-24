using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Campaigns.Commands.CreatePointCampaign;
using LoyaltyCloud.Application.Notifications.Custom.Queries.PreviewCustomNotificationAudience;
using LoyaltyCloud.Common.Security;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Domain.ValueObjects;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class MTLevel5DynamicCampaignAudienceTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public MTLevel5DynamicCampaignAudienceTests(CustomWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        await EnsureTenantOperationalAsync();
        await EnsureExtendedLevelsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "MTLevel5")]
    public async Task Point_campaign_all_levels_applies_to_any_dynamic_level()
    {
        var campaign = await CreateCampaignAsync(PointCampaign.CampaignLevelEligibilityAll);

        var best = await WithTenantAsync(sp =>
            sp.GetRequiredService<IPointCampaignRepository>().GetBestApplicableAsync(
                DateTime.UtcNow,
                100m,
                "Diamond",
                CancellationToken.None));

        Assert.NotNull(best);
        Assert.Equal(campaign.Value.Id, best!.Id);
    }

    [Theory]
    [InlineData("Platinum", "Platinum", true)]
    [InlineData("Platinum", "Diamond", true)]
    [InlineData("Platinum", "Radiance", false)]
    [Trait("Category", "MTLevel5")]
    public async Task Point_campaign_minimum_dynamic_level_uses_sort_order(
        string requiredLevel,
        string customerLevel,
        bool expectedEligible)
    {
        await CreateCampaignAsync(requiredLevel);

        var best = await WithTenantAsync(sp =>
            sp.GetRequiredService<IPointCampaignRepository>().GetBestApplicableAsync(
                DateTime.UtcNow,
                100m,
                customerLevel,
                CancellationToken.None));

        Assert.Equal(expectedEligible, best is not null);
    }

    [Fact]
    [Trait("Category", "MTLevel5")]
    public void Point_campaign_supports_fourth_and_fifth_levels()
    {
        var campaign = new PointCampaign(
            Guid.NewGuid(),
            TenantSeed.KBeautyTenantId,
            "Diamond campaign",
            "Fifth level campaign.",
            2,
            null,
            "Diamond",
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(6),
            DateTime.UtcNow);
        var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mist"] = 1,
            ["Glow"] = 2,
            ["Radiance"] = 3,
            ["Platinum"] = 4,
            ["Diamond"] = 5
        };

        Assert.False(campaign.AppliesToLevel("Platinum", ranks));
        Assert.True(campaign.AppliesToLevel("Diamond", ranks));
    }

    [Fact]
    [Trait("Category", "MTLevel5")]
    public async Task Point_campaign_rejects_unknown_or_other_tenant_level()
    {
        var unknown = await CreateCampaignAsync("Emerald");
        var otherTenantLevel = await CreateCampaignAsync("Bella Oro");

        Assert.True(unknown.IsFailure);
        Assert.True(otherTenantLevel.IsFailure);
    }

    [Fact]
    [Trait("Category", "MTLevel5")]
    public async Task Custom_audience_all_returns_all_wallet_customers()
    {
        await SeedCardAsync("KB-MTL5-A", "Radiance");
        await SeedCardAsync("KB-MTL5-B", "Diamond");

        var preview = await PreviewAudienceAsync(CustomNotificationCampaign.AudienceAllWalletUsers);

        Assert.True(preview.Value.TotalRecipients >= 2);
        Assert.Contains(preview.Value.SampleRecipients, r => r.SerialNumber == "KB-MTL5-A");
        Assert.Contains(preview.Value.SampleRecipients, r => r.SerialNumber == "KB-MTL5-B");
    }

    [Fact]
    [Trait("Category", "MTLevel5")]
    public async Task Custom_audience_minimum_dynamic_level_filters_by_sort_order()
    {
        await SeedCardAsync("KB-MTL5-C", "Radiance");
        await SeedCardAsync("KB-MTL5-D", "Platinum");
        await SeedCardAsync("KB-MTL5-E", "Diamond");

        var preview = await PreviewAudienceAsync("Platinum");

        Assert.DoesNotContain(preview.Value.SampleRecipients, r => r.SerialNumber == "KB-MTL5-C");
        Assert.Contains(preview.Value.SampleRecipients, r => r.SerialNumber == "KB-MTL5-D");
        Assert.Contains(preview.Value.SampleRecipients, r => r.SerialNumber == "KB-MTL5-E");
    }

    [Fact]
    [Trait("Category", "MTLevel5")]
    public async Task Custom_audience_rejects_other_tenant_level()
    {
        var preview = await PreviewAudienceAsync("Bella Oro");

        Assert.True(preview.IsFailure);
    }

    [Fact]
    [Trait("Category", "MTLevel5")]
    public void Campaign_and_audience_admin_pages_use_dynamic_level_options()
    {
        var root = GetRepositoryRoot();
        var campaigns = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "Campaigns.razor"));
        var marketing = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "MarketingNotifications.razor"));

        Assert.Contains("levelOptions", campaigns);
        Assert.Contains("TenantLevels.GetActiveLevelsAsync", campaigns);
        Assert.DoesNotContain("CampaignLevelEligibility.Mist", campaigns);
        Assert.DoesNotContain("CampaignLevelEligibility.Glow", campaigns);
        Assert.DoesNotContain("CampaignLevelEligibility.Radiance", campaigns);

        Assert.Contains("levelOptions", marketing);
        Assert.Contains("TenantLevels.GetActiveLevelsAsync", marketing);
        Assert.DoesNotContain("CustomNotificationAudienceType.MistAndAbove", marketing);
        Assert.DoesNotContain("CustomNotificationAudienceType.GlowAndAbove", marketing);
        Assert.DoesNotContain("CustomNotificationAudienceType.RadianceOnly", marketing);
    }

    [Fact]
    [Trait("Category", "MTLevel5")]
    public void Campaigns_and_audiences_have_no_fixed_level_rank_switches()
    {
        var root = GetRepositoryRoot();
        var campaignRuntime = string.Join(
            Environment.NewLine,
            File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Domain", "Entities", "PointCampaign.cs")),
            File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Infrastructure", "Repositories", "PointCampaignRepository.cs")),
            File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Infrastructure", "Services", "PointCampaignNotificationReadService.cs")),
            File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Infrastructure", "Services", "CustomNotificationAudienceReadService.cs")));

        Assert.DoesNotContain("LevelRank(", campaignRuntime);
        Assert.DoesNotContain("EligibilityRank(", campaignRuntime);
        Assert.DoesNotContain("LoyaltyConstants.Levels.Mist", campaignRuntime);
        Assert.DoesNotContain("LoyaltyConstants.Levels.Glow", campaignRuntime);
        Assert.DoesNotContain("LoyaltyConstants.Levels.Radiance", campaignRuntime);
    }

    private async Task<LoyaltyCloud.Common.Results.Result<LoyaltyCloud.Application.Campaigns.PointCampaignAdminDto>> CreateCampaignAsync(
        string levelEligibility,
        int multiplier = 2)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        return await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(new CreatePointCampaignCommand(
            $"MT5 {levelEligibility} {unique}",
            "MT-Level-5 dynamic campaign.",
            multiplier,
            null,
            levelEligibility,
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(6),
            true)));
    }

    private async Task<LoyaltyCloud.Common.Results.Result<LoyaltyCloud.Application.Notifications.Custom.CustomNotificationAudiencePreviewDto>> PreviewAudienceAsync(
        string audienceType) =>
        await WithTenantAsync(sp => sp.GetRequiredService<ISender>().Send(new PreviewCustomNotificationAudienceQuery(
            audienceType,
            null,
            null,
            100)));

    private async Task SeedCardAsync(string serial, string levelName)
    {
        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            if (await db.LoyaltyCards.AnyAsync(c => c.SerialNumber == serial))
                return;

            var now = DateTime.UtcNow;
            var customer = new Customer(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                $"MT5 {serial}",
                $"{serial.ToLowerInvariant()}@test.local",
                new DateTime(1990, 1, 1),
                now,
                phone: null);
            var card = new LoyaltyCard(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                customer.Id,
                serial,
                now);
            var level = await db.TenantLoyaltyLevels.SingleAsync(l => l.Name == levelName);
            card.ApplyCalculatedLevel(new MemberLevel(level.Id, level.Name, level.Threshold, int.MaxValue, level.SortOrder), new FixedClock(now));

            db.Customers.Add(customer);
            db.LoyaltyCards.Add(card);
            db.DeviceRegistrations.Add(new DeviceRegistration(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                $"device-{serial}",
                "pass.com.kbeautymx.loyalty",
                serial,
                $"push-{serial}",
                now));
            await db.SaveChangesAsync();
        });
    }

    private async Task EnsureTenantOperationalAsync()
    {
        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == TenantSeed.KBeautyTenantId);
            db.Entry(subscription).Property(nameof(TenantSubscription.PaidThroughUtc)).CurrentValue = DateTime.UtcNow.AddDays(30);
            await db.SaveChangesAsync();
        });
    }

    private async Task EnsureExtendedLevelsAsync()
    {
        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            if (await db.TenantLoyaltyLevels.AnyAsync(l => l.Name == "Diamond"))
                return;

            var now = DateTime.UtcNow;
            db.TenantLoyaltyLevels.AddRange(
                new TenantLoyaltyLevel(Guid.Parse("b1000000-0000-0000-0000-000000000105"), TenantSeed.KBeautyTenantId, "Platinum", 4000, 4, now),
                new TenantLoyaltyLevel(Guid.Parse("b1000000-0000-0000-0000-000000000106"), TenantSeed.KBeautyTenantId, "Diamond", 7000, 5, now));
            await db.SaveChangesAsync();
        });

        await WithTenantAsync(Guid.Parse("b2000000-0000-0000-0000-000000000001"), "bella", async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            if (await db.TenantLoyaltyLevels.AnyAsync(l => l.Name == "Bella Oro"))
                return;

            db.TenantLoyaltyLevels.Add(new TenantLoyaltyLevel(
                Guid.Parse("b2000000-0000-0000-0000-000000000106"),
                Guid.Parse("b2000000-0000-0000-0000-000000000001"),
                "Bella Oro",
                500,
                1,
                DateTime.UtcNow));
            await db.SaveChangesAsync();
        });
    }

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

    private async Task WithTenantAsync(Guid tenantId, string tenantSlug, Func<IServiceProvider, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, tenantSlug);
        await action(scope.ServiceProvider);
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
