using System.Collections.Concurrent;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Env = LoyaltyCloud.Tests.Integration.TenantProvisioningTests.ProvisioningTestEnvironment;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampVisualAssets")]
public sealed class StampVisualAssetsTests
{
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
        Assert.Single(refresh.TenantCalls);
        Assert.All(refresh.CardCalls, c => Assert.Equal(PassUpdateReason.StampEarned, c.Reason));
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
        Assert.Equal(3, refresh.TenantCalls.Count);
        Assert.All(refresh.TenantCalls, x => Assert.Equal(a, x));
        Assert.Equal(1, await env.TenantReadAsync(a, "asset-a", db => db.StampProgramVersions.CountAsync()));
        await env.TenantReadAsync(a, "asset-a", async db =>
        {
            (await db.TenantAdminUsers.SingleAsync()).ChangeRole(TenantUserRole.Cashier); await db.SaveChangesAsync(); return true;
        });
        Assert.True((await env.InTenantScopeAsync(a, "asset-a", sp => sp.GetRequiredService<IStampVisualService>()
            .UpdateAsync(StampImageKind.Empty, null, Png(16, 16)))).IsFailure);
    }

    [Fact]
    public async Task Earn_and_reversal_request_refresh_after_commit_but_reward_redemption_does_not()
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
        Assert.True(redeemed.IsSuccess); Assert.Equal(calls, refresh.CardCalls.Count);
        Assert.Contains(refresh.CardCalls, x => x.Reason == PassUpdateReason.StampReversed);
        refresh.Fail = true;
        Assert.True((await env.SendAsOperatorAsync(id, "refresh-stamp", new EarnStampCommand(serial, "apns-down"))).IsSuccess);
        Assert.Equal(6, (await Projection(env, id, "refresh-stamp", serial)).CurrentStamps);
    }

    private static Task<Env> EnvironmentAsync(MemoryAssets store, RecordingRefresh refresh) => Env.CreateAsync(configure: services =>
    {
        services.RemoveAll<IStampVisualAssetStore>(); services.AddSingleton<IStampVisualAssetStore>(store);
        services.RemoveAll<IAppleWalletPassRefreshService>(); services.AddSingleton<IAppleWalletPassRefreshService>(refresh);
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
}
