using System.Data.Common;
using System.Text;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;
using Moq;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Env = LoyaltyCloud.Tests.Integration.TenantProvisioningTests.ProvisioningTestEnvironment;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "PlatformTenantDeletion")]
public sealed class StampTenantDeletionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stamps_hard_delete_removes_all_dependencies_and_preserves_other_tenant(bool verifyRollback)
    {
        var failure = new FailCardDelete();
        await using var env = await Env.CreateAsync(configure: services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(failure)));
        var target = await SeedAsync(env, "delete-stamps", "6465550101");
        var other = await SeedAsync(env, "keep-stamps", "6465550102");
        var targetBefore = await CaptureAsync(env, target);
        var otherBefore = await CaptureAsync(env, other);
        foreach (var table in new[] { "StampProgramVersions", "StampRewardDefinitions", "StampRewardIdentities",
            "StampVisualConfigurations", "StampCycles", "StampTransactions", "EarnedStampRewards",
            "StampOperationReceipts", "StampPassEventContexts", "Customers", "LoyaltyCards" })
            Assert.NotEqual("[]", targetBefore[table]);

        if (verifyRollback)
        {
            failure.Enabled = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => env.DeleteTenantAsync(target, "delete-stamps"));
            failure.Enabled = false;
            AssertSnapshotsEqual(targetBefore, await CaptureAsync(env, target));
            AssertSnapshotsEqual(otherBefore, await CaptureAsync(env, other));
        }

        var result = await env.DeleteTenantAsync(target, "delete-stamps");
        Assert.True(result.IsSuccess, result.Error);
        foreach (var row in await CaptureAsync(env, target))
            Assert.True(row.Value == "[]", $"Remaining tenant data in {row.Key}: {row.Value}");
        AssertSnapshotsEqual(otherBefore, await CaptureAsync(env, other));
    }

    [Fact]
    public async Task Nonrelational_fallback_removes_stamps_without_removing_other_tenant()
    {
        var context = new TenantContext();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options, Mock.Of<IPublisher>(), context);
        var a = await SeedTrackedAsync(db, context, "tracked-a");
        var b = await SeedTrackedAsync(db, context, "tracked-b");
        context.Clear();
        db.ChangeTracker.Clear();
        var service = new SuperAdminTenantManagementService(db, context, new DateTimeProvider(),
            NullLogger<SuperAdminTenantManagementService>.Instance);
        var result = await service.DeleteAsync(a, "tracked-a");
        Assert.True(result.IsSuccess, result.Error);
        Assert.False(await db.Tenants.AnyAsync(x => x.Id == a));
        Assert.True(await db.Tenants.AnyAsync(x => x.Id == b));
        Assert.Equal(new int[9], await StampCountsAsync(db, a));
        Assert.Equal(Enumerable.Repeat(1, 9), await StampCountsAsync(db, b));
        Assert.False(await db.Customers.IgnoreQueryFilters().AnyAsync(x => x.TenantId == a));
        Assert.False(await db.LoyaltyCards.IgnoreQueryFilters().AnyAsync(x => x.TenantId == a));
        Assert.True(await db.Customers.IgnoreQueryFilters().AnyAsync(x => x.TenantId == b));
        Assert.True(await db.LoyaltyCards.IgnoreQueryFilters().AnyAsync(x => x.TenantId == b));
    }

    private static async Task<Guid> SeedTrackedAsync(AppDbContext db, TenantContext context, string slug)
    {
        context.Clear();
        var id = Guid.NewGuid(); var now = DateTime.UtcNow; var adminId = Guid.NewGuid();
        context.SetTenant(id, slug);
        var customer = new Customer(Guid.NewGuid(), id, "Ana", slug + "@example.com", Customer.BirthdayNotCaptured, now);
        var card = new LoyaltyCard(Guid.NewGuid(), id, customer.Id, slug.ToUpperInvariant(), now);
        var program = new StampProgramVersion(Guid.NewGuid(), id, 1, 10, 0, StampReversalPolicy.AdminOnly, adminId, now);
        var definition = new StampRewardDefinition(Guid.NewGuid(), program, 1, "Reward", null);
        var identity = new StampRewardIdentity(definition.RewardIdentityId, id);
        identity.SetImage($"tenant-branding/{id}/available.png");
        identity.SetUpcomingImage($"tenant-branding/{id}/upcoming.png");
        var cycle = new StampCycle(Guid.NewGuid(), card, program, now);
        cycle.Earn(program, now);
        var earn = StampTransaction.Earn(cycle, adminId, "earn", 1, now);
        db.AddRange(new Tenant(id, slug, slug, "America/Tijuana", now, programType: LoyaltyProgramType.Stamps),
            new TenantAdminUser(adminId, id, "owner", "test-only-hash", now), customer, card, program, definition,
            identity, cycle, earn, new EarnedStampReward(cycle, earn, definition, now),
            new StampVisualConfiguration(id), new StampPassEventContext(card.SerialNumber, earn, false),
            new StampOperationReceipt(id, "earn", "Earn", card.SerialNumber, null, adminId, "{}", now));
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<int[]> StampCountsAsync(AppDbContext db, Guid id) =>
    [
        await db.StampProgramVersions.IgnoreQueryFilters().CountAsync(x => x.TenantId == id),
        await db.StampRewardDefinitions.IgnoreQueryFilters().CountAsync(x => x.TenantId == id),
        await db.StampRewardIdentities.IgnoreQueryFilters().CountAsync(x => x.TenantId == id),
        await db.StampVisualConfigurations.IgnoreQueryFilters().CountAsync(x => x.TenantId == id),
        await db.StampCycles.IgnoreQueryFilters().CountAsync(x => x.TenantId == id),
        await db.StampTransactions.IgnoreQueryFilters().CountAsync(x => x.TenantId == id),
        await db.EarnedStampRewards.IgnoreQueryFilters().CountAsync(x => x.TenantId == id),
        await db.StampOperationReceipts.IgnoreQueryFilters().CountAsync(x => x.TenantId == id),
        await db.StampPassEventContexts.IgnoreQueryFilters().CountAsync(x => x.TenantId == id)
    ];

    private static async Task<Guid> SeedAsync(Env env, string slug, string phone)
    {
        var provisioned = await env.ProvisionAsync(slug, "Stamps delete QA", "owner", "Owner123!",
            programType: LoyaltyProgramType.Stamps);
        Assert.True(provisioned.IsSuccess, provisioned.Error);
        var id = provisioned.Value.TenantId;
        var program = await env.ConfigureStampsAsync(id, slug, welcome: 2);
        Assert.True(program.IsSuccess, program.Error);
        var joined = await env.JoinAsync(id, slug, "Ana", "Stamps", phone);
        Assert.True(joined.IsSuccess, joined.Error);
        var serial = joined.Value.SerialNumber;
        for (var i = 0; i < 3; i++)
            Assert.True((await env.SendAsOperatorAsync(id, slug, new EarnStampCommand(serial, "first-" + i))).IsSuccess);
        var reversed = await env.SendAsOperatorAsync(id, slug, new ReverseLastStampCommand(serial, "reverse"));
        Assert.True(reversed.IsSuccess, reversed.Error);
        var unlocked = await env.SendAsOperatorAsync(id, slug, new EarnStampCommand(serial, "unlock-again"));
        Assert.True(unlocked.IsSuccess, unlocked.Error);
        var redeemed = await env.SendAsOperatorAsync(id, slug,
            new RedeemStampRewardCommand(serial, unlocked.Value.UnlockedRewards.Single().Id, "redeem"));
        Assert.True(redeemed.IsSuccess, redeemed.Error);
        for (var i = 0; i < 5; i++)
            Assert.True((await env.SendAsOperatorAsync(id, slug, new EarnStampCommand(serial, "complete-" + i))).IsSuccess);
        var nextVersion = await env.ConfigureStampsAsync(id, slug, welcome: 0);
        Assert.True(nextVersion.IsSuccess, nextVersion.Error);
        Assert.NotEqual(program.Value.Id, nextVersion.Value.Id);
        var rollover = await env.SendAsOperatorAsync(id, slug, new EarnStampCommand(serial, "rollover"));
        Assert.True(rollover.IsSuccess, rollover.Error);
        Assert.Equal(2, rollover.Value.State.CycleSequence);
        await env.TenantWriteAsync(id, slug, async db =>
        {
            foreach (var identity in await db.StampRewardIdentities.ToListAsync())
            {
                identity.SetImage($"tenant-branding/{id}/stamps/{identity.Id}-available.png");
                identity.SetUpcomingImage($"tenant-branding/{id}/stamps/{identity.Id}-upcoming.png");
            }
            var visuals = new StampVisualConfiguration(id);
            visuals.SetImage(false, $"tenant-branding/{id}/stamps/empty.png");
            visuals.SetImage(true, $"tenant-branding/{id}/stamps/filled.png");
            db.StampVisualConfigurations.Add(visuals);
            await db.SaveChangesAsync();
            var statuses = await db.EarnedStampRewards.Select(x => x.Status).Distinct().ToListAsync();
            Assert.Contains(EarnedStampRewardStatus.Available, statuses);
            Assert.Contains(EarnedStampRewardStatus.Redeemed, statuses);
            Assert.Contains(EarnedStampRewardStatus.Voided, statuses);
            Assert.True(await db.StampTransactions.AnyAsync(x => x.ReversesTransactionId != null));
        });
        return id;
    }

    // Compare every scalar column in every mapped tenant table, including rowversions.
    // Read SQL directly so tenant query filters cannot hide orphan rows or changes to B.
    private static Task<Dictionary<string, string>> CaptureAsync(Env env, Guid tenantId) =>
        env.PlatformReadAsync(async db =>
        {
            var snapshot = new Dictionary<string, string>();
            await db.Database.OpenConnectionAsync();
            foreach (var entity in db.Model.GetEntityTypes().Where(e => e.FindProperty("TenantId") != null || e.ClrType == typeof(Tenant)))
            {
                var table = entity.GetTableName()!;
                var tenantColumn = entity.ClrType == typeof(Tenant) ? "Id" : "TenantId";
                var order = string.Join(",", entity.FindPrimaryKey()!.Properties.Select(p => Quote(p.Name)));
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = $"SELECT * FROM {Quote(entity.GetSchema() ?? "dbo")}.{Quote(table)} WHERE {Quote(tenantColumn)} = @tenant ORDER BY {order} FOR JSON PATH, INCLUDE_NULL_VALUES";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@tenant"; parameter.Value = tenantId;
                command.Parameters.Add(parameter);
                await using var reader = await command.ExecuteReaderAsync();
                var json = new StringBuilder();
                while (await reader.ReadAsync()) json.Append(reader.GetString(0));
                snapshot.Add(table, json.Length == 0 ? "[]" : json.ToString());
            }
            return snapshot;
        });

    private static string Quote(string identifier) => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static void AssertSnapshotsEqual(Dictionary<string, string> expected, Dictionary<string, string> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(x => x), actual.Keys.OrderBy(x => x));
        foreach (var row in expected) Assert.Equal(row.Value, actual[row.Key]);
    }

    private sealed class FailCardDelete : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("[LoyaltyCards]", StringComparison.Ordinal))
                throw new InvalidOperationException("Simulated failure after Stamps deletion.");
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
