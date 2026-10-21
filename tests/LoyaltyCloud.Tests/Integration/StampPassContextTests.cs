using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Application.Customers.Commands.JoinCustomer;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Env = LoyaltyCloud.Tests.Integration.TenantProvisioningTests.ProvisioningTestEnvironment;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampContext")]
public sealed class StampPassContextTests
{
    private const string Slug = "stamp-context";
    private sealed class Clock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
        public DateTime Today => UtcNow.Date;
    }
    private static Task<Env> Create(Clock clock) => Env.CreateAsync(configure: services =>
    {
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.RemoveAll<IDateTimeProvider>(); services.AddSingleton<IDateTimeProvider>(clock);
        services.RemoveAll<IStampVisualAssetStore>(); services.AddSingleton<IStampVisualAssetStore>(new StampVisualAssetsTests.MemoryAssets());
    });
    private static async Task<(Guid Id, string Serial)> Setup(Env env, int welcome = 0)
    {
        var id = (await env.ProvisionAsync(Slug, "Stamps", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        var configured = await env.SendAsOperatorAsync(id, Slug, new SetStampProgramCommand(3, welcome, StampReversalPolicy.AdminAndCashier,
            [new(2, "Una bebida mediana gratis", ShortName: "Bebida gratis"), new(3, "Un café gratis", ShortName: "Café gratis")]));
        Assert.True(configured.IsSuccess, configured.Error);
        return (id, (await env.JoinAsync(id, Slug, "Ana", "Test", "6465550001")).Value.SerialNumber);
    }
    private static Task<StampWalletProjection> Read(Env env, Guid id, string serial) => env.InTenantScopeAsync(id, Slug,
        sp => sp.GetRequiredService<IStampWalletProjectionService>().ReadAsync(serial), authenticate: false);

    [Fact]
    public async Task Earn_unlock_reversal_expiration_and_repeated_gets_use_authoritative_event()
    {
        var clock = new Clock(); await using var env = await Create(clock); var (id, serial) = await Setup(env);
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "one"));
        var first = (await Read(env, id, serial)).Event!;
        Assert.Equal("Earn", first.Operation); Assert.False(first.RewardUnlocked); Assert.False(first.IsRollover);
        Assert.Equal("🎉 ¡Conseguiste un sello más! Ahora tienes %@.", first.ChangeMessage);
        Assert.Equal(first, (await Read(env, id, serial)).Event);
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "two"));
        var unlocked = (await Read(env, id, serial)).Event!;
        Assert.True(unlocked.RewardUnlocked); Assert.Equal("Bebida gratis", unlocked.Value);
        Assert.Equal("🎁 ¡Desbloqueaste Bebida gratis! Ahora tienes %@.", unlocked.ChangeMessage);
        await env.SendAsOperatorAsync(id, Slug, new ReverseLastStampCommand(serial, "reverse"));
        var reversed = await Read(env, id, serial);
        Assert.Equal("Reversal", reversed.Event!.Operation);
        Assert.Equal("Se canceló el último sello. Ahora tienes %@.", reversed.Event.ChangeMessage);
        Assert.Equal(StampWalletRewardState.Upcoming, reversed.Rewards.Single(x => x.RequiredStamps == 2).State);
        clock.UtcNow += StampPassEventContext.Lifetime;
        Assert.Null((await Read(env, id, serial)).Event);
        await env.SendAsOperatorAsync(id, Slug, new ReverseLastStampCommand(serial, "reverse"));
        Assert.Null((await Read(env, id, serial)).Event); // retry never extends expiry
        Assert.Equal(1, await env.TenantReadAsync(id, Slug, db => db.StampPassEventContexts.CountAsync()));
    }

    [Fact]
    public async Task Visual_changes_and_redeem_suppress_events_retries_never_reactivate_them()
    {
        var clock = new Clock(); await using var env = await Create(clock); var (id, serial) = await Setup(env, 1);
        var earn = await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "unlock"));
        var reward = Assert.Single(earn.Value.UnlockedRewards);
        Assert.NotNull((await Read(env, id, serial)).Event);
        await env.InTenantScopeAsync(id, Slug, sp => sp.GetRequiredService<IStampVisualService>()
            .UpdateAsync(StampImageKind.Empty, null, StampVisualAssetsTests.Png(24, 24)));
        Assert.Null((await Read(env, id, serial)).Event);
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "unlock"));
        Assert.Null((await Read(env, id, serial)).Event);
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "final"));
        Assert.NotNull((await Read(env, id, serial)).Event);
        var redeemed = await env.SendAsOperatorAsync(id, Slug, new RedeemStampRewardCommand(serial, reward.Id, "redeem"));
        Assert.True(redeemed.IsSuccess, redeemed.Error);
        var state = await Read(env, id, serial);
        Assert.Null(state.Event); Assert.Equal(StampWalletRewardState.Redeemed, state.Rewards.Single(x => x.RequiredStamps == 2).State);
        Assert.Empty(await env.TenantReadAsync(id, Slug, db => db.LoyaltyNotifications.ToListAsync()));
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "rollover"));
        var newer = (await Read(env, id, serial)).Event;
        await env.SendAsOperatorAsync(id, Slug, new RedeemStampRewardCommand(serial, reward.Id, "redeem"));
        Assert.Equal(newer, (await Read(env, id, serial)).Event); // stale redeem replay cannot clear a new Earn
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Rollover_and_historical_definition_ignore_active_rule_edits(bool unlockOnRollover)
    {
        var clock = new Clock(); await using var env = await Create(clock); var (id, serial) = await Setup(env, 1);
        var original = (await env.SendAsOperatorAsync(id, Slug, new GetActiveStampProgramQuery()))!;
        await env.SendAsOperatorAsync(id, Slug, new SetStampProgramCommand(2, 0, StampReversalPolicy.None,
            unlockOnRollover ? [new(1, "Nueva recompensa", ShortName: "Nueva"), new(2, "Final", ShortName: "Final")]
                : [new(2, "Final", ShortName: "Final")]));
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "old-unlock"));
        var old = await Read(env, id, serial);
        Assert.Equal(original.Id, old.VersionId); Assert.Equal("Bebida gratis", old.Event!.Value);
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "old-final"));
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "rollover"));
        var rolled = await Read(env, id, serial);
        Assert.True(rolled.Event!.IsRollover); Assert.Equal(unlockOnRollover, rolled.Event.RewardUnlocked);
        Assert.Equal(unlockOnRollover ? "Nueva" : "1 / 2", rolled.Event.Value);
        Assert.NotEqual(original.Id, rolled.VersionId);
    }

    [Fact]
    public async Task Multiple_devices_receive_same_context_and_branding_suppresses_for_all()
    {
        var clock = new Clock(); await using var env = await Create(clock); var (id, serial) = await Setup(env);
        await env.TenantReadAsync(id, Slug, async db =>
        {
            for (var n = 1; n <= 2; n++) db.Add(new DeviceRegistration(Guid.NewGuid(), id, $"device-{n}", "pass.test", serial, $"token-{n}", clock.UtcNow));
            await db.SaveChangesAsync(); return true;
        });
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "one"));
        var snapshots = await Task.WhenAll(Read(env, id, serial), Read(env, id, serial));
        Assert.Equal(snapshots[0].Event, snapshots[1].Event); Assert.NotNull(snapshots[0].Event);
        var result = await env.InTenantScopeAsync(id, Slug, async sp =>
        {
            var db = sp.GetRequiredService<LoyaltyCloud.Infrastructure.Persistence.AppDbContext>();
            var cardId = await db.LoyaltyCards.Select(x => x.Id).SingleAsync();
            return await sp.GetRequiredService<IAppleWalletPassRefreshService>().RefreshCardAsync(id, cardId, PassUpdateReason.BrandingUpdated);
        });
        Assert.Equal(2, result.DevicesFound); Assert.Equal(2, result.PushesAttempted);
        Assert.Null((await Read(env, id, serial)).Event);
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "one"));
        Assert.Null((await Read(env, id, serial)).Event);
    }

    [Fact]
    public async Task Short_name_is_versioned_trimmed_and_snapshotted_without_changing_identity()
    {
        var clock = new Clock(); await using var env = await Create(clock); var (id, serial) = await Setup(env, 1);
        var v1 = (await env.SendAsOperatorAsync(id, Slug, new GetActiveStampProgramQuery()))!;
        var input = v1.Rewards.Select(r => r with { ShortName = "  " + r.ShortName + "  " }).ToList();
        var noop = await env.SendAsOperatorAsync(id, Slug, new SetStampProgramCommand(3, 1, v1.ReversalPolicy, input));
        Assert.Equal(v1.Id, noop.Value.Id);
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "old-reward"));
        input[0] = input[0] with { ShortName = "Nuevo nombre" };
        var v2 = await env.SendAsOperatorAsync(id, Slug, new SetStampProgramCommand(3, 1, v1.ReversalPolicy, input));
        Assert.NotEqual(v1.Id, v2.Value.Id); Assert.Equal(v1.Rewards[0].RewardIdentityId, v2.Value.Rewards[0].RewardIdentityId);
        Assert.Equal("Bebida gratis", (await Read(env, id, serial)).Event!.Value);
        await env.TenantReadAsync(id, Slug, async db =>
        {
            Assert.Equal("Bebida gratis", (await db.EarnedStampRewards.SingleAsync()).ShortName);
            Assert.Equal("Nuevo nombre", (await db.StampRewardDefinitions.SingleAsync(x => x.StampProgramVersionId == v2.Value.Id && x.RequiredStamps == 2)).ShortName);
            return true;
        });
    }

    [Fact]
    public async Task Short_name_migration_backfills_definitions_and_snapshots_independently()
    {
        await using var env = await Env.CreateAsync("20261002010407_AddStampRewardIdentityAndVisualAssets");
        var id = (await env.ProvisionAsync(Slug, "Stamps", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        var definitionId = Guid.NewGuid(); var rewardId = Guid.NewGuid();
        await env.TenantReadAsync(id, Slug, async db =>
        {
            var userId = await db.TenantAdminUsers.Select(x => x.Id).SingleAsync();
            var now = DateTime.UtcNow;
            var version = new StampProgramVersion(Guid.NewGuid(), id, 1, 2, 0, StampReversalPolicy.None, userId, now);
            var customer = new Customer(Guid.NewGuid(), id, "Test", "test@local.test", Customer.BirthdayNotCaptured, now, "6465550009");
            var card = new LoyaltyCard(Guid.NewGuid(), id, customer.Id, "ST-BACKFILL", now);
            var cycle = new StampCycle(Guid.NewGuid(), card, version, now); cycle.Earn(version, now);
            var transaction = StampTransaction.Earn(cycle, userId, "legacy", 1, now);
            var identity = new StampRewardIdentity(Guid.NewGuid(), id);
            db.AddRange(version, customer, card, cycle, transaction); await db.SaveChangesAsync();
            // Seed the historical schema, before UpcomingImageBlobName existed.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO StampRewardIdentities (Id,TenantId) VALUES ({identity.Id},{id})");
            var longName = new string('A', 100);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO StampRewardDefinitions (Id,TenantId,StampProgramVersionId,RewardIdentityId,RequiredStamps,Name) VALUES ({definitionId},{id},{version.Id},{identity.Id},{1},{longName})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO EarnedStampRewards (Id,TenantId,StampCycleId,SourceEarnTransactionId,StampRewardDefinitionId,Name,RequiredStamps,Status,EarnedAt) VALUES ({rewardId},{id},{cycle.Id},{transaction.Id},{definitionId},{"Snapshot anterior"},{1},{"Available"},{now})");
            return true;
        });
        await env.PlatformReadAsync(async db => { await db.Database.MigrateAsync(); return true; });
        await env.TenantReadAsync(id, Slug, async db =>
        {
            Assert.Equal(new string('A', 40), (await db.StampRewardDefinitions.SingleAsync()).ShortName);
            Assert.Equal("Snapshot anterior", (await db.EarnedStampRewards.SingleAsync()).ShortName);
            Assert.Empty(await db.StampPassEventContexts.ToListAsync()); return true;
        });
    }

    [Fact]
    public async Task Manual_message_remains_in_existing_notification_pipeline_and_suppresses_old_stamp_context()
    {
        var clock = new Clock(); await using var env = await Create(clock); var (id, serial) = await Setup(env);
        await env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, "earn"));
        var cardId = await env.TenantReadAsync(id, Slug, async db =>
        {
            var card = await db.LoyaltyCards.SingleAsync();
            var notification = new LoyaltyNotification(Guid.NewGuid(), id, card.CustomerId, card.Id, NotificationType.Custom,
                "Aviso", "Detalle", clock.UtcNow, null, clock.UtcNow.AddDays(1), null, "manual", null,
                shortMessage: "Mensaje corto", longMessage: "Detalle largo");
            notification.MarkCompleted(NotificationStatus.Delivered, clock.UtcNow);
            db.Add(notification); await db.SaveChangesAsync(); return card.Id;
        });
        await env.InTenantScopeAsync(id, Slug, sp => sp.GetRequiredService<IAppleWalletPassRefreshService>()
            .RefreshCardAsync(id, cardId, PassUpdateReason.LevelChanged)); // Existing Custom channel reason.
        Assert.Null((await Read(env, id, serial)).Event);
        var manual = await env.InTenantScopeAsync(id, Slug, sp => sp.GetRequiredService<IWalletNotificationReadService>().GetActiveContextAsync(cardId));
        Assert.Equal("Mensaje corto", manual.CustomMessage!.ShortMessage);
        Assert.Equal("Detalle largo", manual.CustomMessage.LongMessage);
        Assert.Equal(NotificationType.Custom, manual.RecentVisibleEvent!.Type);
        Assert.Single(await env.TenantReadAsync(id, Slug, db => db.LoyaltyNotifications.ToListAsync()));
    }

    [Fact]
    public async Task Context_and_visual_suppression_are_tenant_scoped()
    {
        var clock = new Clock(); await using var env = await Create(clock); var (a, serialA) = await Setup(env);
        var b = (await env.ProvisionAsync("stamp-other", "Other", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        await env.ConfigureStampsAsync(b, "stamp-other", 0);
        var serialB = (await env.JoinAsync(b, "stamp-other", "Bea", "Test", "6465550001")).Value.SerialNumber;
        await env.SendAsOperatorAsync(a, Slug, new EarnStampCommand(serialA, "earn-a"));
        await env.SendAsOperatorAsync(b, "stamp-other", new EarnStampCommand(serialB, "earn-b"));
        await env.TenantReadAsync(a, Slug, async db =>
        {
            Assert.Single(await db.StampPassEventContexts.ToListAsync());
            var foreign = await db.StampPassEventContexts.IgnoreQueryFilters().SingleAsync(x => x.TenantId == b);
            var transaction = await db.StampTransactions.SingleAsync(x => x.Type == StampTransactionType.Earn);
            db.Entry(foreign).Property(x => x.TransactionId).CurrentValue = transaction.Id;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); return true;
        });
        await env.InTenantScopeAsync(a, Slug, sp => sp.GetRequiredService<IStampVisualService>().UpdateAsync(StampImageKind.Filled, null, null));
        Assert.Null((await Read(env, a, serialA)).Event);
        var other = await env.InTenantScopeAsync(b, "stamp-other", sp => sp.GetRequiredService<IStampWalletProjectionService>().ReadAsync(serialB));
        Assert.NotNull(other.Event);
    }

    [Theory]
    [InlineData(null, null)] [InlineData(29, 2)]
    public async Task Birthday_is_persisted_on_join_and_reentry_preserves_card_and_birthday(int? day, int? month)
    {
        var clock = new Clock(); await using var env = await Create(clock); var (id, _) = await Setup(env);
        var joined = await env.SendAsync(id, Slug, new JoinCustomerCommand("Eva", "Join", "6465550002", day, month));
        Assert.True(joined.IsSuccess, joined.Error);
        var recovered = await env.SendAsync(id, Slug, new JoinCustomerCommand("Eva", "Join", "6465550002", 1, 1));
        Assert.True(recovered.Value.AlreadyExists); Assert.Equal(joined.Value.SerialNumber, recovered.Value.SerialNumber);
        await env.TenantReadAsync(id, Slug, async db =>
        {
            var customer = await db.Customers.SingleAsync(x => x.Id == joined.Value.CustomerId);
            Assert.Equal(day.HasValue ? new DateTime(2000, month!.Value, day.Value) : Customer.BirthdayNotCaptured, customer.DateOfBirth);
            Assert.Equal(1, await db.LoyaltyCards.CountAsync(x => x.CustomerId == customer.Id)); return true;
        });
    }
}
