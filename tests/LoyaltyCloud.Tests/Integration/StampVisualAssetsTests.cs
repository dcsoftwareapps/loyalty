using System.Collections.Concurrent;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Env = LoyaltyCloud.Tests.Integration.TenantProvisioningTests.ProvisioningTestEnvironment;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampVisualAssets")]
public sealed class StampVisualAssetsTests
{
    [Fact]
    public async Task Stamp_icon_change_refreshes_installed_cards_and_is_tenant_scoped_without_duplicate_updates()
    {
        var store = new MemoryAssets(); var apple = new RecordingRefresh();
        var google = new Mock<IGoogleStampWalletService>();
        google.Setup(x => x.SynchronizeBySerialNumberIfExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await using var env = await EnvironmentAsync(store, apple, google);
        var a = (await env.ProvisionAsync("art-refresh-a", "A", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        var b = (await env.ProvisionAsync("art-refresh-b", "B", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        await env.ConfigureStampsAsync(a, "art-refresh-a");
        await env.ConfigureStampsAsync(b, "art-refresh-b");
        var serialA = (await env.JoinAsync(a, "art-refresh-a", "A", "Member", "6465550031")).Value.SerialNumber;
        var serialB = (await env.JoinAsync(b, "art-refresh-b", "B", "Member", "6465550032")).Value.SerialNumber;
        var cardA = await RegisterGoogleAsync(env, a, "art-refresh-a", serialA);
        await env.TenantReadAsync(a, "art-refresh-a", async db =>
        {
            db.DeviceRegistrations.Add(new DeviceRegistration(Guid.NewGuid(), a, "device-a", "pass.test.loyalty",
                serialA, "push-a", DateTime.UtcNow));
            await db.SaveChangesAsync(); return true;
        });
        await RegisterGoogleAsync(env, b, "art-refresh-b", serialB);
        var before = StampStripRenderer.Render(await Projection(env, a, "art-refresh-a", serialA), "#FFFFFF")[0].Bytes;
        var newImage = Png(80, 80, new Rgba32(17, 91, 219));
        var result = await env.InTenantScopeAsync(a, "art-refresh-a", sp =>
            sp.GetRequiredService<IStampVisualService>().UpdateAsync(StampImageKind.Empty, null, newImage));
        Assert.True(result.IsSuccess, result.Error);
        var after = await Projection(env, a, "art-refresh-a", serialA);
        Assert.Equal(StampVisualService.NormalizeImage(newImage), after.EmptyImage);
        Assert.NotEqual(before, StampStripRenderer.Render(after, "#FFFFFF")[0].Bytes);
        Assert.Contains(apple.CardCalls, call => call.Id == cardA && call.Reason == PassUpdateReason.BrandingUpdated);
        google.Verify(x => x.SynchronizeBySerialNumberIfExistsAsync(serialA, It.IsAny<CancellationToken>()), Times.Once);
        google.Verify(x => x.SynchronizeBySerialNumberIfExistsAsync(serialB, It.IsAny<CancellationToken>()), Times.Never);

        var uploads = store.Count;
        result = await env.InTenantScopeAsync(a, "art-refresh-a", sp =>
            sp.GetRequiredService<IStampVisualService>().UpdateAsync(StampImageKind.Empty, null, newImage));
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(uploads, store.Count);
        Assert.Single(apple.CardCalls, call => call.Reason == PassUpdateReason.BrandingUpdated);
        google.Verify(x => x.SynchronizeBySerialNumberIfExistsAsync(serialA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reward_image_change_refreshes_only_cards_whose_current_cycle_uses_the_identity()
    {
        var apple = new RecordingRefresh(); var google = new Mock<IGoogleStampWalletService>();
        google.Setup(x => x.SynchronizeBySerialNumberIfExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await using var env = await EnvironmentAsync(new MemoryAssets(), apple, google);
        const string slug = "reward-refresh";
        var tenant = (await env.ProvisionAsync(slug, "Rewards", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        var old = await env.SendAsOperatorAsync(tenant, slug, new SetStampProgramCommand(3, 0, StampReversalPolicy.None, [new(3, "Old", null)]));
        var oldSerial = (await env.JoinAsync(tenant, slug, "Old", "Member", "6465550041")).Value.SerialNumber;
        await RegisterGoogleAsync(env, tenant, slug, oldSerial);
        var current = await env.SendAsOperatorAsync(tenant, slug, new SetStampProgramCommand(3, 0, StampReversalPolicy.None, [new(3, "New", null)]));
        var currentIdentity = current.Value.Rewards.Single().RewardIdentityId!.Value;
        var currentSerial = (await env.JoinAsync(tenant, slug, "New", "Member", "6465550042")).Value.SerialNumber;
        var currentCard = await RegisterGoogleAsync(env, tenant, slug, currentSerial);
        var image = Png(72, 72, new Rgba32(19, 186, 87));
        var result = await env.InTenantScopeAsync(tenant, slug, sp =>
            sp.GetRequiredService<IStampVisualService>().UpdateAsync(StampImageKind.Reward, currentIdentity, image));
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(old.Value.Id, (await Projection(env, tenant, slug, oldSerial)).VersionId);
        Assert.Null((await Projection(env, tenant, slug, oldSerial)).Rewards.Single().Image);
        Assert.Equal(StampVisualService.NormalizeImage(image), (await Projection(env, tenant, slug, currentSerial)).Rewards.Single().Image);
        Assert.Contains(apple.CardCalls, call => call.Id == currentCard && call.Reason == PassUpdateReason.BrandingUpdated);
        google.Verify(x => x.SynchronizeBySerialNumberIfExistsAsync(currentSerial, It.IsAny<CancellationToken>()), Times.Once);
        google.Verify(x => x.SynchronizeBySerialNumberIfExistsAsync(oldSerial, It.IsAny<CancellationToken>()), Times.Never);

        var upcoming = Png(72, 72, new Rgba32(70, 31, 177));
        result = await env.InTenantScopeAsync(tenant, slug, sp =>
            sp.GetRequiredService<IStampVisualService>().UpdateAsync(StampImageKind.UpcomingReward, currentIdentity, upcoming));
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(StampVisualService.NormalizeImage(upcoming),
            (await Projection(env, tenant, slug, currentSerial)).Rewards.Single().UpcomingImage);
        google.Verify(x => x.SynchronizeBySerialNumberIfExistsAsync(currentSerial, It.IsAny<CancellationToken>()), Times.Exactly(2));
        google.Verify(x => x.SynchronizeBySerialNumberIfExistsAsync(oldSerial, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Wallet_failure_does_not_undo_saved_art_and_is_logged()
    {
        var logs = new RecordingLogProvider();
        await using var env = await Env.CreateAsync(configure: services =>
        {
            services.RemoveAll<IStampVisualAssetStore>(); services.AddSingleton<IStampVisualAssetStore>(new MemoryAssets());
            services.RemoveAll<IStampWalletRefreshService>(); services.AddSingleton<IStampWalletRefreshService>(new ThrowingRefresh());
            services.AddLogging(builder => builder.AddProvider(logs));
        });
        const string slug = "art-failure";
        var tenant = (await env.ProvisionAsync(slug, "Failure", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        await env.ConfigureStampsAsync(tenant, slug);
        var serial = (await env.JoinAsync(tenant, slug, "A", "Member", "6465550051")).Value.SerialNumber;
        await RegisterGoogleAsync(env, tenant, slug, serial);
        var image = Png(60, 60, new Rgba32(190, 60, 30));
        var result = await env.InTenantScopeAsync(tenant, slug, sp =>
            sp.GetRequiredService<IStampVisualService>().UpdateAsync(StampImageKind.Filled, null, image));
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(StampVisualService.NormalizeImage(image), (await Projection(env, tenant, slug, serial)).FilledImage);
        Assert.Contains(logs.Messages, message => message.Contains("Stamps visual refresh failed after saving assets"));
    }

    [Fact]
    public async Task Optional_upcoming_upload_and_removal_preserve_available_art_and_historical_rules()
    {
        var store = new MemoryAssets(); var refresh = new RecordingRefresh();
        await using var env = await EnvironmentAsync(store, refresh);
        const string slug = "upcoming-art";
        var id = (await env.ProvisionAsync(slug, "Upcoming", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        var rules = await env.SendAsOperatorAsync(id, slug, new SetStampProgramCommand(10, 4, StampReversalPolicy.None, [new(10, "Reward", null)]));
        var identity = rules.Value.Rewards.Single().RewardIdentityId!.Value;
        var serial = (await env.JoinAsync(id, slug, "Old", "Card", "6465550014")).Value.SerialNumber;
        var available = StampVisualService.NormalizeImage(Png(50, 50, new Rgba32(220, 40, 80)));
        var upcoming = StampVisualService.NormalizeImage(Png(50, 50, new Rgba32(20, 160, 230)));
        async Task Update(StampImageKind kind, byte[]? bytes)
        {
            var result = await env.InTenantScopeAsync(id, slug, sp => sp.GetRequiredService<IStampVisualService>().UpdateAsync(kind, identity, bytes));
            Assert.True(result.IsSuccess, result.Error);
        }
        Task<StampVisualDto> Preview() => env.InTenantScopeAsync(id, slug, sp => sp.GetRequiredService<IStampVisualService>().ReadAsync());
        await Update(StampImageKind.Reward, available);
        var before = await Projection(env, id, slug, serial);
        Assert.Null(before.Rewards.Single().UpcomingImage);
        Assert.False((await Preview()).UpcomingRewardPreviews[identity].IsCustom);
        string? availablePath = null;
        await env.TenantReadAsync(id, slug, async db =>
        {
            var row = await db.StampRewardIdentities.SingleAsync();
            availablePath = row.ImageBlobName; Assert.NotNull(availablePath); Assert.Null(row.UpcomingImageBlobName);
            return true;
        });
        // A current visual identity is shared with historical rules; no economic snapshot changes.
        var changed = await env.SendAsOperatorAsync(id, slug, new SetStampProgramCommand(8, 0, StampReversalPolicy.None, [new(8, "Reward", null, identity)]));
        Assert.True(changed.IsSuccess, changed.Error);
        await Update(StampImageKind.UpcomingReward, upcoming);
        var custom = await Projection(env, id, slug, serial);
        Assert.Equal(rules.Value.Id, custom.VersionId); Assert.Equal(10, custom.Rewards.Single().RequiredStamps);
        Assert.Equal(available, custom.Rewards.Single().Image); Assert.Equal(upcoming, custom.Rewards.Single().UpcomingImage);
        var preview = (await Preview()).UpcomingRewardPreviews[identity]; Assert.True(preview.IsCustom);
        Assert.Equal(upcoming, Convert.FromBase64String(preview.Preview.Split(',')[1]));
        var uploads = store.Count;
        await Update(StampImageKind.UpcomingReward, null);
        Assert.Equal(uploads, store.Count); // NULL is meaningful, never upload a synthetic fallback.
        var after = await Projection(env, id, slug, serial);
        Assert.Equal(before.Rewards.Single().Image, after.Rewards.Single().Image); Assert.Null(after.Rewards.Single().UpcomingImage);
        Assert.Equal(StampStripRenderer.Render(before, "#FFFFFF")[0].Bytes, StampStripRenderer.Render(after, "#FFFFFF")[0].Bytes);
        var automatic = (await Preview()).UpcomingRewardPreviews[identity]; Assert.False(automatic.IsCustom);
        using var gray = Image.Load<Rgba32>(Convert.FromBase64String(automatic.Preview.Split(',')[1]));
        Assert.Equal(gray[25, 25].R, gray[25, 25].G); Assert.Equal(gray[25, 25].G, gray[25, 25].B);
        await env.TenantReadAsync(id, slug, async db =>
        {
            var row = await db.StampRewardIdentities.SingleAsync(); Assert.Null(row.UpcomingImageBlobName); Assert.Equal(availablePath, row.ImageBlobName);
            Assert.Equal(2, await db.StampProgramVersions.CountAsync()); Assert.Empty(await db.StampPassEventContexts.ToListAsync()); return true;
        });
        Assert.Empty(refresh.TenantCalls);
    }

    [Fact]
    public async Task Wallet_projects_only_cycle_rewards_even_when_active_version_adds_an_intermediate_milestone()
    {
        await using var env = await EnvironmentAsync(new MemoryAssets(), new RecordingRefresh());
        const string slug = "one-reward-grid";
        var tenant = (await env.ProvisionAsync(slug, "Grid", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        var v1 = await env.SendAsOperatorAsync(tenant, slug, new SetStampProgramCommand(10, 4, StampReversalPolicy.None,
            [new(10, "Reward final", null)]));
        Assert.True(v1.IsSuccess, v1.Error);
        var serial = (await env.JoinAsync(tenant, slug, "First", "Card", "6465550011")).Value.SerialNumber;
        var first = await Projection(env, tenant, slug, serial);
        Assert.Equal(4, first.CurrentStamps); Assert.Equal(10, first.MaxStamps);
        Assert.Equal(v1.Value.Id, first.VersionId);
        Assert.Equal(10, Assert.Single(first.Rewards).RequiredStamps);
        Assert.Equal(StampWalletRewardState.Upcoming, first.Rewards.Single().State);

        var v2 = await env.SendAsOperatorAsync(tenant, slug, new SetStampProgramCommand(10, 4, StampReversalPolicy.None,
            [new(5, "Reward intermedia", null), new(10, "Reward final", null, v1.Value.Rewards.Single().RewardIdentityId)]));
        Assert.True(v2.IsSuccess, v2.Error);
        var secondSerial = (await env.JoinAsync(tenant, slug, "Second", "Card", "6465550012")).Value.SerialNumber;
        var second = await Projection(env, tenant, slug, secondSerial);
        Assert.Equal(v2.Value.Id, second.VersionId);
        Assert.Equal(new[] { 5, 10 }, second.Rewards.OrderBy(x => x.RequiredStamps).Select(x => x.RequiredStamps));
        Assert.All(second.Rewards, r => Assert.Equal(StampWalletRewardState.Upcoming, r.State));
        var historical = await Projection(env, tenant, slug, serial);
        Assert.Equal(v1.Value.Id, historical.VersionId);
        Assert.Equal(10, Assert.Single(historical.Rewards).RequiredStamps);
        // Removing milestone 5 from the active program must not rewrite a V2 card.
        var v3 = await env.SendAsOperatorAsync(tenant, slug, new SetStampProgramCommand(10, 4, StampReversalPolicy.None,
            [new(10, "Reward final", null, v1.Value.Rewards.Single().RewardIdentityId)]));
        Assert.True(v3.IsSuccess, v3.Error);
        var thirdSerial = (await env.JoinAsync(tenant, slug, "Third", "Card", "6465550013")).Value.SerialNumber;
        Assert.Equal(10, Assert.Single((await Projection(env, tenant, slug, thirdSerial)).Rewards).RequiredStamps);
        var retained = await Projection(env, tenant, slug, secondSerial);
        Assert.Equal(v2.Value.Id, retained.VersionId);
        Assert.Equal(new[] { 5, 10 }, retained.Rewards.OrderBy(x => x.RequiredStamps).Select(x => x.RequiredStamps));
        Assert.Equal(first.CurrentStamps, historical.CurrentStamps);
        Assert.Equal(StampStripRenderer.Render(first, "#FFFFFF")[0].Bytes,
            StampStripRenderer.Render(historical, "#FFFFFF")[0].Bytes);
    }

    [Fact]
    public async Task Migration_backfills_independent_identities_without_guessing_equal_reward_text()
    {
        await using var env = await Env.CreateAsync("20261001192907_AddStampOperations");
        var id = (await env.ProvisionAsync("backfill-stamps", "A", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        var definitions = new List<Guid>();
        await env.TenantReadAsync(id, "backfill-stamps", async db =>
        {
            var userId = await db.TenantAdminUsers.Select(x => x.Id).SingleAsync();
            for (var i = 1; i <= 2; i++)
            {
                var version = new LoyaltyCloud.Domain.Entities.StampProgramVersion(Guid.NewGuid(), id, i, 10, 0, StampReversalPolicy.None, userId, DateTime.UtcNow);
                if (i == 1) version.Supersede(DateTime.UtcNow);
                db.Add(version); await db.SaveChangesAsync();
                var definition = Guid.NewGuid(); definitions.Add(definition);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO StampRewardDefinitions (Id, TenantId, StampProgramVersionId, RequiredStamps, Name) VALUES ({definition}, {id}, {version.Id}, {10}, {"Same reward"})");
            }
            return true;
        });
        await env.PlatformReadAsync(async db => { await db.Database.MigrateAsync(); return true; });
        await env.TenantReadAsync(id, "backfill-stamps", async db =>
        {
            var rows = await db.StampRewardDefinitions.ToListAsync();
            Assert.Equal(2, rows.Count); Assert.Equal(2, rows.Select(x => x.RewardIdentityId).Distinct().Count());
            Assert.All(rows, r => Assert.Equal(r.Id, r.RewardIdentityId));
            Assert.Equal(2, await db.StampRewardIdentities.CountAsync());
            Assert.All(rows, r => Assert.Equal("Same reward", r.Name));
            return true;
        });
    }

    [Fact]
    public async Task Explicit_identity_survives_edits_and_visuals_follow_historical_cycles_without_changing_snapshots()
    {
        var store = new MemoryAssets(); var refresh = new RecordingRefresh();
        await using var env = await EnvironmentAsync(store, refresh);
        var a = (await env.ProvisionAsync("visual-a", "A", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        var v1 = await env.SendAsOperatorAsync(a, "visual-a", new SetStampProgramCommand(10, 9, StampReversalPolicy.AdminOnly,
            [new(10, "Producto gratis", "Original")]));
        Assert.True(v1.IsSuccess, v1.Error);
        var identity = v1.Value.Rewards.Single().RewardIdentityId!.Value;
        var serial1 = (await env.JoinAsync(a, "visual-a", "Ana", "A", "6465550001")).Value.SerialNumber;
        var earned = await env.SendAsOperatorAsync(a, "visual-a", new EarnStampCommand(serial1, "complete"));
        Assert.True(earned.IsSuccess, earned.Error);
        var rewardId = Assert.Single(earned.Value.UnlockedRewards).Id;
        var v2 = await env.SendAsOperatorAsync(a, "visual-a", new SetStampProgramCommand(8, 2, StampReversalPolicy.AdminOnly,
            [new(8, "Regalo", "Editada", identity)]));
        Assert.Equal(identity, v2.Value.Rewards.Single().RewardIdentityId);
        var serial2 = (await env.JoinAsync(a, "visual-a", "Otra", "A", "6465550002")).Value.SerialNumber;
        Assert.True((await env.SendAsOperatorAsync(a, "visual-a", new EarnStampCommand(serial1, "new-cycle"))).IsSuccess);
        var rollover = await Projection(env, a, "visual-a", serial1);
        Assert.Equal(1, rollover.CurrentStamps); Assert.Equal(8, rollover.MaxStamps);
        // A separate completed V1 card remains full until its own next Earn.
        var historicalSerial = await env.TenantReadAsync(a, "visual-a", async db =>
        {
            var customer = new LoyaltyCloud.Domain.Entities.Customer(Guid.NewGuid(), a, "Old Cycle", "internal@example.local", new DateTime(1990, 1, 1), DateTime.UtcNow, "6465550003");
            var card = new LoyaltyCloud.Domain.Entities.LoyaltyCard(Guid.NewGuid(), a, customer.Id, "ST-HISTORY01", DateTime.UtcNow);
            var old = await db.StampProgramVersions.SingleAsync(x => x.Id == v1.Value.Id);
            var cycle = new LoyaltyCloud.Domain.Entities.StampCycle(Guid.NewGuid(), card, old, DateTime.UtcNow);
            cycle.Earn(old, DateTime.UtcNow);
            db.Add(customer); db.Add(card); db.Add(cycle); await db.SaveChangesAsync(); return card.SerialNumber;
        });
        await env.TenantReadAsync(a, "visual-a", async db =>
        {
            db.DeviceRegistrations.Add(new DeviceRegistration(Guid.NewGuid(), a, "device-old", "pass.test.loyalty",
                historicalSerial, "push-old", DateTime.UtcNow));
            db.DeviceRegistrations.Add(new DeviceRegistration(Guid.NewGuid(), a, "device-new", "pass.test.loyalty",
                serial2, "push-new", DateTime.UtcNow));
            await db.SaveChangesAsync(); return true;
        });
        var bytes = Png(80, 160);
        var upload = await env.InTenantScopeAsync(a, "visual-a", sp => sp.GetRequiredService<IStampVisualService>()
            .UpdateAsync(StampImageKind.Reward, identity, bytes));
        Assert.True(upload.IsSuccess, upload.Error);
        var p1 = await Projection(env, a, "visual-a", historicalSerial);
        var p2 = await Projection(env, a, "visual-a", serial2);
        Assert.Equal(10, p1.CurrentStamps); Assert.Equal(10, p1.MaxStamps);
        Assert.Equal(2, p2.CurrentStamps); Assert.Equal(8, p2.MaxStamps);
        Assert.Equal(10, p1.Rewards.Single().RequiredStamps); Assert.Equal(8, p2.Rewards.Single().RequiredStamps);
        Assert.Equal(p1.Rewards.Single().Image, p2.Rewards.Single().Image);
        Assert.NotNull(p1.Rewards.Single().Image);
        await env.TenantReadAsync(a, "visual-a", async db =>
        {
            Assert.Equal(2, await db.StampProgramVersions.CountAsync());
            var snapshot = await db.EarnedStampRewards.SingleAsync(x => x.Id == rewardId);
            Assert.Equal("Producto gratis", snapshot.Name); Assert.Equal("Original", snapshot.Description); Assert.Equal(10, snapshot.RequiredStamps);
            return true;
        });
        var deleted = await env.SendAsOperatorAsync(a, "visual-a", new SetStampProgramCommand(8, 2, StampReversalPolicy.AdminOnly,
            [new(8, "Temporal")]));
        Assert.True(deleted.IsSuccess);
        var readded = await env.SendAsOperatorAsync(a, "visual-a", new SetStampProgramCommand(8, 2, StampReversalPolicy.AdminOnly,
            [new(8, "Regalo", "Editada")]));
        Assert.NotEqual(identity, readded.Value.Rewards.Single().RewardIdentityId);
        Assert.Equal(p1.Rewards.Single().Image, (await Projection(env, a, "visual-a", historicalSerial)).Rewards.Single().Image);
        Assert.Empty(refresh.TenantCalls);
        Assert.Equal(2, refresh.CardCalls.Count(c => c.Reason == PassUpdateReason.BrandingUpdated));
        Assert.All(refresh.CardCalls.Where(c => c.Reason != PassUpdateReason.BrandingUpdated),
            c => Assert.Equal(PassUpdateReason.StampEarned, c.Reason));
        Assert.DoesNotContain((await Projection(env, a, "visual-a", historicalSerial)).Rewards, r => r.RequiredStamps == 8);
    }

    [Fact]
    public async Task Assets_are_tenant_scoped_validated_and_visual_changes_refresh_without_economic_versions()
    {
        var store = new MemoryAssets(); var refresh = new RecordingRefresh();
        await using var env = await EnvironmentAsync(store, refresh);
        var a = (await env.ProvisionAsync("asset-a", "A", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        var b = (await env.ProvisionAsync("asset-b", "B", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        var v1 = (await env.ConfigureStampsAsync(a, "asset-a")).Value;
        await env.ConfigureStampsAsync(b, "asset-b");
        var identity = v1.Rewards[0].RewardIdentityId;
        Assert.True((await env.InTenantScopeAsync(b, "asset-b", sp => sp.GetRequiredService<IStampVisualService>()
            .UpdateAsync(StampImageKind.Reward, identity, Png(16, 16)))).IsFailure);
        var otherPreview = await env.InTenantScopeAsync(b, "asset-b", sp => sp.GetRequiredService<IStampVisualService>().ReadAsync());
        Assert.DoesNotContain(identity!.Value, otherPreview.RewardPreviews.Keys);
        await env.TenantReadAsync(b, "asset-b", async db =>
        {
            var version = await db.StampProgramVersions.SingleAsync(x => x.IsActive);
            db.Add(new LoyaltyCloud.Domain.Entities.StampRewardDefinition(Guid.NewGuid(), version, 3, "Foreign asset", null, identity));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); return true;
        });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => env.InTenantScopeAsync(a, "asset-a",
            sp => sp.GetRequiredService<IStampVisualService>().ReadAsync(), authenticate: false));
        Assert.True((await env.SendAsOperatorAsync(b, "asset-b", new SetStampProgramCommand(10, 0, StampReversalPolicy.None,
            [new(10, "Foreign", RewardIdentityId: identity)]))).IsFailure);
        foreach (var bytes in new[] { new byte[] { 1, 2, 3 }, new byte[2 * 1024 * 1024 + 1], Png(4097, 1) })
            Assert.True((await env.InTenantScopeAsync(a, "asset-a", sp => sp.GetRequiredService<IStampVisualService>()
                .UpdateAsync(StampImageKind.Empty, null, bytes))).IsFailure);
        foreach (var kind in new[] { StampImageKind.Empty, StampImageKind.Filled })
            Assert.True((await env.InTenantScopeAsync(a, "asset-a", sp => sp.GetRequiredService<IStampVisualService>()
                .UpdateAsync(kind, null, Png(160, 40)))).IsSuccess);
        Assert.True((await env.InTenantScopeAsync(a, "asset-a", sp => sp.GetRequiredService<IStampVisualService>()
            .UpdateAsync(StampImageKind.Empty, null, null))).IsSuccess);
        Assert.Empty(refresh.TenantCalls);
        Assert.Equal(1, await env.TenantReadAsync(a, "asset-a", db => db.StampProgramVersions.CountAsync()));
        await env.TenantReadAsync(a, "asset-a", async db =>
        {
            (await db.TenantAdminUsers.SingleAsync()).ChangeRole(TenantUserRole.Cashier); await db.SaveChangesAsync(); return true;
        });
        Assert.True((await env.InTenantScopeAsync(a, "asset-a", sp => sp.GetRequiredService<IStampVisualService>()
            .UpdateAsync(StampImageKind.Empty, null, Png(16, 16)))).IsFailure);
    }

    [Fact]
    public async Task Earn_reversal_and_reward_redemption_request_refresh_after_commit()
    {
        var refresh = new RecordingRefresh();
        await using var env = await EnvironmentAsync(new MemoryAssets(), refresh);
        var id = (await env.ProvisionAsync("refresh-stamp", "A", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        await env.ConfigureStampsAsync(id, "refresh-stamp", 4);
        var serial = (await env.JoinAsync(id, "refresh-stamp", "Ana", "A", "6465550001")).Value.SerialNumber;
        await env.SendAsOperatorAsync(id, "refresh-stamp", new EarnStampCommand(serial, "earn1"));
        await env.SendAsOperatorAsync(id, "refresh-stamp", new ReverseLastStampCommand(serial, "reverse"));
        var earned = await env.SendAsOperatorAsync(id, "refresh-stamp", new EarnStampCommand(serial, "earn2"));
        var calls = refresh.CardCalls.Count;
        var redeemed = await env.SendAsOperatorAsync(id, "refresh-stamp", new RedeemStampRewardCommand(serial, earned.Value.UnlockedRewards.Single().Id, "redeem"));
        Assert.True(redeemed.IsSuccess); Assert.Equal(calls + 1, refresh.CardCalls.Count);
        Assert.Equal(PassUpdateReason.StampRewardRedeemed, refresh.CardCalls.Last().Reason);
        Assert.Null((await Projection(env, id, "refresh-stamp", serial)).Event);
        Assert.Equal(StampWalletRewardState.Redeemed, (await Projection(env, id, "refresh-stamp", serial)).Rewards.Single(x => x.RequiredStamps == 5).State);
        Assert.Contains(refresh.CardCalls, x => x.Reason == PassUpdateReason.StampReversed);
        refresh.Fail = true;
        Assert.True((await env.SendAsOperatorAsync(id, "refresh-stamp", new EarnStampCommand(serial, "apns-down"))).IsSuccess);
        Assert.Equal(6, (await Projection(env, id, "refresh-stamp", serial)).CurrentStamps);
    }

    private static async Task<Guid> RegisterGoogleAsync(Env env, Guid tenantId, string slug, string serial) =>
        await env.TenantReadAsync(tenantId, slug, async db =>
        {
            var card = await db.LoyaltyCards.SingleAsync(x => x.SerialNumber == serial);
            db.MemberDigitalWallets.Add(new MemberDigitalWallet(Guid.NewGuid(), tenantId, card.CustomerId, card.Id,
                DigitalWalletProvider.Google, $"issuer.{tenantId:N}", $"issuer.{card.Id:N}", DateTime.UtcNow));
            await db.SaveChangesAsync();
            return card.Id;
        });

    private static Task<Env> EnvironmentAsync(MemoryAssets store, RecordingRefresh refresh,
        Mock<IGoogleStampWalletService>? google = null) => Env.CreateAsync(configure: services =>
    {
        services.RemoveAll<IStampVisualAssetStore>(); services.AddSingleton<IStampVisualAssetStore>(store);
        services.RemoveAll<IAppleWalletPassRefreshService>(); services.AddSingleton<IAppleWalletPassRefreshService>(refresh);
        if (google is not null)
        {
            services.RemoveAll<IGoogleStampWalletService>();
            services.AddSingleton(google.Object);
        }
    });
    private static Task<StampWalletProjection> Projection(Env env, Guid id, string slug, string serial) => env.InTenantScopeAsync(id, slug,
        sp => sp.GetRequiredService<IStampWalletProjectionService>().ReadAsync(serial), authenticate: false);
    internal static byte[] Png(int width, int height, Rgba32? color = null)
    {
        using var image = new Image<Rgba32>(width, height, color ?? new Rgba32(220, 40, 80));
        using var stream = new MemoryStream(); image.SaveAsPng(stream); return stream.ToArray();
    }
    internal sealed class MemoryAssets : IStampVisualAssetStore
    {
        private readonly ConcurrentDictionary<(Guid, string), byte[]> _images = new();
        public int Count => _images.Count;
        public Task<string> UploadAsync(Guid tenantId, byte[] png, CancellationToken ct)
        {
            var name = $"{tenantId:N}/{Guid.NewGuid():N}.png"; _images[(tenantId, name)] = png; return Task.FromResult(name);
        }
        public Task<byte[]?> ReadAsync(Guid tenantId, string? blobName, CancellationToken ct) =>
            Task.FromResult(blobName is not null && _images.TryGetValue((tenantId, blobName), out var image) ? image : null);
    }
    internal sealed class RecordingRefresh : IAppleWalletPassRefreshService
    {
        public List<(Guid Id, PassUpdateReason Reason)> CardCalls { get; } = [];
        public List<Guid> TenantCalls { get; } = [];
        public bool Fail { get; set; }
        public Task<AppleWalletPassRefreshResult> RefreshCardAsync(Guid tenantId, Guid loyaltyCardId, PassUpdateReason reason, CancellationToken ct = default)
        { CardCalls.Add((loyaltyCardId, reason)); return Result(tenantId); }
        public Task<AppleWalletPassRefreshResult> RefreshTenantInstalledPassesAsync(Guid tenantId, PassUpdateReason reason, CancellationToken ct = default)
        { TenantCalls.Add(tenantId); return Result(tenantId); }
        private Task<AppleWalletPassRefreshResult> Result(Guid id) => Fail ? throw new IOException("APNs unavailable")
            : Task.FromResult(new AppleWalletPassRefreshResult(id, [], 0, 0, 0, 0, 0, false, ApnPushFailureType.None, null));
    }

    private sealed class ThrowingRefresh : IStampWalletRefreshService
    {
        public Task RefreshCardAsync(Guid tenantId, Guid loyaltyCardId, string serialNumber,
            PassUpdateReason reason, CancellationToken ct = default) => throw new IOException("Wallet unavailable");
    }

    private sealed class RecordingLogProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);
        public void Dispose() { }

        private sealed class RecordingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }
}
