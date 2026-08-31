using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GiftCardPersistenceTests
{
    private static readonly Guid TenantId = Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("92000000-0000-0000-0000-000000000001");
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DuplicateRedemptionKey_ChangesBalanceAndLedgerOnlyOnce()
    {
        await using var database = await TestDatabase.CreateAsync(500m);
        await using var db = database.Context();
        var service = Service(db);

        var first = await service.RedeemAsync("GC-TEST-IDEMPOTENT", 100m, "same-request", null, null);
        var duplicate = await service.RedeemAsync("GC-TEST-IDEMPOTENT", 100m, "same-request", null, null);

        Assert.True(first.Success);
        Assert.True(duplicate.Success);
        Assert.True(duplicate.WasIdempotent);
        Assert.Equal(400m, (await db.GiftCards.AsNoTracking().SingleAsync()).CurrentBalance);
        Assert.Equal(1, await db.GiftCardTransactions.CountAsync(t => t.Type == GiftCardTransactionType.Redeemed));
    }

    [Fact]
    public async Task ConcurrentRedemptions_AllowOnlyOneAndPersistFinalBalance()
    {
        await using var database = await TestDatabase.CreateAsync(300m);
        await using var dbA = database.Context();
        await using var dbB = database.Context();
        var results = await Task.WhenAll(
            Service(dbA).RedeemAsync("GC-TEST-IDEMPOTENT", 200m, "request-a", null, null),
            Service(dbB).RedeemAsync("GC-TEST-IDEMPOTENT", 200m, "request-b", null, null));

        Assert.Single(results, r => r.Success);
        Assert.Single(results, r => !r.Success);
        await using var verify = database.Context();
        Assert.Equal(100m, (await verify.GiftCards.AsNoTracking().SingleAsync()).CurrentBalance);
        Assert.Equal(1, await verify.GiftCardTransactions.CountAsync(t => t.Type == GiftCardTransactionType.Redeemed));
    }

    private static GiftCardService Service(AppDbContext db)
    {
        var clock = new Mock<IDateTimeProvider>(); clock.SetupGet(x => x.UtcNow).Returns(Now); clock.SetupGet(x => x.Today).Returns(Now.Date);
        var user = new Mock<ICurrentUserService>(); user.SetupGet(x => x.UserId).Returns(UserId.ToString());
        var google = new Mock<IGiftCardWalletService>(); google.Setup(x => x.SynchronizeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var apple = new Mock<IGiftCardAppleWalletService>(); apple.Setup(x => x.SynchronizeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var tenant = new Mock<ITenantContext>(); tenant.SetupGet(x => x.TenantId).Returns(TenantId); tenant.SetupGet(x => x.HasTenant).Returns(true);
        return new GiftCardService(db, tenant.Object, clock.Object, user.Object, google.Object, apple.Object);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private TestDatabase(DbContextOptions<AppDbContext> options) => _options = options;

        public static async Task<TestDatabase> CreateAsync(decimal balance)
        {
            var name = $"LoyaltyCloudGiftCardTests_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True;")
                .Options;
            var result = new TestDatabase(options);
            await using var db = result.Context();
            await db.Database.EnsureCreatedAsync();
            db.Tenants.Add(new Tenant(TenantId, "giftcard-test", "Gift Card Test", "UTC", Now));
            db.TenantSubscriptions.Add(new TenantSubscription(TenantId, TenantSubscriptionStatus.Active, "test", paidThroughUtc: Now.AddYears(1)));
            var config = new GiftCardConfiguration(Guid.NewGuid(), TenantId, Now);
            config.Update(true, true, true, true, GiftCardExpirationMode.Never, null, "MXN", "Gift Card", "#111111", "#FFFFFF", null, null, null, null, Now);
            var card = new GiftCard(Guid.NewGuid(), TenantId, "GC-TEST-IDEMPOTENT", GiftCard.HashClaimToken("claim"), balance, "MXN", null,
                "Cliente", null, null, null, null, GiftCardSource.Manual, UserId, Now, null);
            db.Add(config); db.Add(card);
            db.Add(new GiftCardTransaction(Guid.NewGuid(), TenantId, card.Id, GiftCardTransactionType.Issued, balance, 0, balance, UserId, Now));
            await db.SaveChangesAsync();
            return result;
        }

        public AppDbContext Context()
        {
            var tenant = new Mock<ITenantContext>(); tenant.SetupGet(x => x.TenantId).Returns(TenantId); tenant.SetupGet(x => x.HasTenant).Returns(true);
            return new AppDbContext(_options, new Mock<IPublisher>().Object, tenant.Object);
        }

        public async ValueTask DisposeAsync()
        {
            await using var db = Context();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
