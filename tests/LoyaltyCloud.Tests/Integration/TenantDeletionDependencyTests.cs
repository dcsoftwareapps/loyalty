using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Env = LoyaltyCloud.Tests.Integration.TenantProvisioningTests.ProvisioningTestEnvironment;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "PlatformTenantDeletion")]
public sealed class TenantDeletionDependencyTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Tokens")]
    [InlineData("Wallets")]
    [InlineData("Gifts")]
    [InlineData("Payments")]
    public async Task Each_dependency_group_can_be_hard_deleted(string group)
    {
        await using var env = await Env.CreateAsync();
        var id = await SeedBaseAsync(env, "delete-dependency", "6465550201");
        await env.TenantWriteAsync(id, "delete-dependency", db => SeedGroupAsync(db, id, group));
        var result = await env.DeleteTenantAsync(id, "delete-dependency");
        Assert.True(result.IsSuccess, result.Error);
        AssertEmpty(await StampTenantDeletionTests.CaptureAsync(env, id));
    }

    [Theory]
    [InlineData(LoyaltyProgramType.Points)]
    [InlineData(LoyaltyProgramType.Stamps)]
    public async Task Complete_delete_rolls_back_then_retries_and_keeps_other_tenant_unchanged(LoyaltyProgramType program)
    {
        var failure = new FailTenantDelete();
        await using var env = await Env.CreateAsync(configure: services =>
            services.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(failure)));
        var a = program == LoyaltyProgramType.Stamps
            ? await StampTenantDeletionTests.SeedAsync(env, "delete-all", "6465550201")
            : await SeedBaseAsync(env, "delete-all", "6465550201");
        var b = program == LoyaltyProgramType.Stamps
            ? await StampTenantDeletionTests.SeedAsync(env, "keep-all", "6465550202")
            : await SeedBaseAsync(env, "keep-all", "6465550202");
        await env.TenantWriteAsync(a, "delete-all", db => SeedGroupAsync(db, a, "All"));
        await env.TenantWriteAsync(b, "keep-all", db => SeedGroupAsync(db, b, "All"));
        var webhook = Guid.NewGuid();
        await env.PlatformReadAsync(async db =>
        {
            db.PaymentWebhookEvents.Add(new PaymentWebhookEvent(webhook, PaymentProvider.Stripe, webhook.ToString(), "invoice.paid", DateTime.UtcNow));
            await db.SaveChangesAsync(); return true;
        });
        var beforeA = await StampTenantDeletionTests.CaptureAsync(env, a);
        var beforeB = await StampTenantDeletionTests.CaptureAsync(env, b);
        failure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.DeleteTenantAsync(a, "delete-all"));
        failure.Enabled = false;
        StampTenantDeletionTests.AssertSnapshotsEqual(beforeA, await StampTenantDeletionTests.CaptureAsync(env, a));
        StampTenantDeletionTests.AssertSnapshotsEqual(beforeB, await StampTenantDeletionTests.CaptureAsync(env, b));
        var result = await env.DeleteTenantAsync(a, "delete-all");
        Assert.True(result.IsSuccess, result.Error);
        AssertEmpty(await StampTenantDeletionTests.CaptureAsync(env, a));
        StampTenantDeletionTests.AssertSnapshotsEqual(beforeB, await StampTenantDeletionTests.CaptureAsync(env, b));
        Assert.True(await env.PlatformReadAsync(db => db.PaymentWebhookEvents.AnyAsync(x => x.Id == webhook)));
    }

    [Fact]
    public async Task Tracked_path_removes_all_dependencies_including_unloaded_cascade_rows()
    {
        var context = new TenantContext();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options, Mock.Of<IPublisher>(), context);
        var a = await StampTenantDeletionTests.SeedTrackedAsync(db, context, "tracked-a");
        await SeedGroupAsync(db, a, "All");
        var b = await StampTenantDeletionTests.SeedTrackedAsync(db, context, "tracked-b");
        await SeedGroupAsync(db, b, "All");
        var beforeB = CaptureTracked(db, b);
        context.Clear(); db.ChangeTracker.Clear();
        var service = new SuperAdminTenantManagementService(db, context, new DateTimeProvider(), NullLogger<SuperAdminTenantManagementService>.Instance);
        var result = await service.DeleteAsync(a, "tracked-a");
        Assert.True(result.IsSuccess, result.Error);
        AssertEmpty(CaptureTracked(db, a));
        StampTenantDeletionTests.AssertSnapshotsEqual(beforeB, CaptureTracked(db, b));
    }

    [Fact]
    public void Audit_entire_mapped_model_without_connecting_to_database()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ModelOnly;Trusted_Connection=True").Options);
        foreach (var entity in db.Model.GetEntityTypes().OrderBy(e => e.GetTableName()))
        {
            var fks = entity.GetForeignKeys().Select(f => string.Join(",", f.Properties.Select(p => p.Name)) + " -> " + f.PrincipalEntityType.GetTableName() + " (" + f.DeleteBehavior + ")");
            output.WriteLine(entity.GetTableName() + " | " + (entity.FindProperty("TenantId") != null ? "tenant" : "global") + " | " + string.Join("; ", fks));
        }
    }

    private static async Task<Guid> SeedBaseAsync(Env env, string slug, string phone)
    {
        var tenant = await env.ProvisionAsync(slug, "Delete dependencies", "owner", "Owner123!");
        Assert.True(tenant.IsSuccess, tenant.Error);
        var joined = await env.JoinAsync(tenant.Value.TenantId, slug, "Ana", "Delete", phone);
        Assert.True(joined.IsSuccess, joined.Error);
        return tenant.Value.TenantId;
    }

    private static async Task SeedGroupAsync(AppDbContext db, Guid id, string group)
    {
        var now = DateTime.UtcNow;
        var admin = await db.TenantAdminUsers.IgnoreQueryFilters().SingleAsync(x => x.TenantId == id);
        var card = await db.LoyaltyCards.IgnoreQueryFilters().SingleAsync(x => x.TenantId == id);
        if (group is "Tokens" or "All")
            db.CashierRefreshTokens.Add(new CashierRefreshToken(id, admin.Id, id.ToString("N") + id.ToString("N"), now, now.AddDays(30)));
        if (group is "Wallets" or "All")
            db.MemberDigitalWallets.Add(new MemberDigitalWallet(Guid.NewGuid(), id, card.CustomerId, card.Id,
                DigitalWalletProvider.Apple, "test-class", id.ToString(), now));
        if (group is "Gifts" or "All")
        {
            db.GiftCardConfigurations.Add(new GiftCardConfiguration(Guid.NewGuid(), id, now));
            db.GiftCardDenominations.Add(new GiftCardDenomination(Guid.NewGuid(), id, 100, "MXN", now));
            var gift = new GiftCard(Guid.NewGuid(), id, Guid.NewGuid().ToString("N"), id.ToString("N") + id.ToString("N"), 100, "MXN",
                card.CustomerId, "Ana", null, null, null, null, GiftCardSource.Manual, admin.Id, now, null);
            db.GiftCards.Add(gift);
            db.GiftCardTransactions.Add(new GiftCardTransaction(Guid.NewGuid(), id, gift.Id, GiftCardTransactionType.Issued, 100, 0, 100, admin.Id, now));
            var balance = gift.Redeem(25, true, now);
            db.GiftCardTransactions.Add(new GiftCardTransaction(Guid.NewGuid(), id, gift.Id, GiftCardTransactionType.Redeemed, 25, balance.Before, balance.After, admin.Id, now, idempotencyKey: "redeem"));
            db.GiftCardWallets.Add(new GiftCardWallet(Guid.NewGuid(), id, gift.Id, GiftCardWalletProvider.Apple, "test-gift", gift.Id.ToString(), now, "test-only-token"));
            db.GiftCardDeviceRegistrations.Add(new GiftCardDeviceRegistration(Guid.NewGuid(), id, gift.Id, id.ToString(), "pass.test", gift.Id.ToString(), "test-only-push", now));
        }
        if (group is "Payments" or "All")
        {
            var order = new BillingOrder(Guid.NewGuid(), id, "trial", 1, 100, 16, 116, "MXN", BillingPaymentMethod.BankTransfer, now, now, now.AddMonths(1));
            order.MarkPaid("platform", now);
            db.BillingOrders.Add(order);
            db.PaymentTransactions.Add(new PaymentTransaction(Guid.NewGuid(), order, Guid.NewGuid().ToString(), now));
            db.TenantBillingProfiles.Add(new TenantBillingProfile(Guid.NewGuid(), id));
        }
        if (group == "All")
        {
            var tenant = await db.Tenants.SingleAsync(x => x.Id == id);
            var attempt = new SelfServiceSignupAttempt(Guid.NewGuid(), Guid.NewGuid().ToString(), "test-fingerprint", "test-only-hash", now);
            attempt.Complete(id, tenant.Slug, admin.Id, now.AddDays(14), now);
            db.SelfServiceSignupAttempts.Add(attempt);
        }
        await db.SaveChangesAsync();
    }

    private static Dictionary<string, string> CaptureTracked(AppDbContext db, Guid id)
    {
        var result = new Dictionary<string, string>();
        foreach (var entity in db.Model.GetEntityTypes().Where(e => e.FindProperty("TenantId") != null || e.ClrType == typeof(Tenant)))
        {
            var method = typeof(TenantDeletionDependencyTests).GetMethod(nameof(ReadTracked), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(entity.ClrType);
            result.Add(entity.GetTableName()!, (string)method.Invoke(null, [db, id])!);
        }
        return result;
    }
    private static string ReadTracked<T>(AppDbContext db, Guid id) where T : class => JsonSerializer.Serialize(
        db.Set<T>().IgnoreQueryFilters().AsNoTracking().AsEnumerable()
            .Where(x => (Guid?)typeof(T).GetProperty(typeof(T) == typeof(Tenant) ? "Id" : "TenantId")!.GetValue(x) == id)
            .OrderBy(x => typeof(T).GetProperty("Id")?.GetValue(x)?.ToString() ?? id.ToString()));
    private static void AssertEmpty(Dictionary<string, string> snapshot)
    {
        foreach (var row in snapshot) Assert.True(row.Value == "[]", "Orphans in " + row.Key);
    }
    private sealed class FailTenantDelete : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("DELETE", StringComparison.Ordinal) && command.CommandText.Contains("[Tenants]", StringComparison.Ordinal))
                throw new InvalidOperationException("Simulated failure before final tenant deletion.");
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
