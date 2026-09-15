using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GiftCardRetryTests
{
    [Theory]
    [InlineData(false, 25, true)]
    [InlineData(false, 26, false)]
    [InlineData(true, 25, false)]
    public async Task Replay_checks_card_and_amount(bool otherCard, decimal amount, bool success)
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.Service.RedeemAsync("GC-AAAA-BBBB-CCCC", 25, "key", null, null);
        Assert.True(first.Success);
        var replay = await fixture.Service.RedeemAsync(otherCard ? "GC-DDDD-EEEE-FFFF" : "GC-AAAA-BBBB-CCCC", amount, "key", null, null);
        Assert.Equal(success, replay.Success);
        Assert.Equal(success, replay.WasIdempotent);
        if (!success) Assert.Equal(GiftCardFailure.IdempotencyConflict, replay.Failure);
        Assert.Single(await fixture.Db.GiftCardTransactions.ToListAsync());
        Assert.Equal(75m, (await fixture.Db.GiftCards.AsNoTracking().SingleAsync(x => x.PublicCode == "GC-AAAA-BBBB-CCCC")).CurrentBalance);
    }

    [Fact]
    public async Task Adjustment_key_cannot_be_replayed_as_redemption()
    {
        await using var fixture = await Fixture.CreateAsync();
        var card = await fixture.Db.GiftCards.FirstAsync();
        Assert.True((await fixture.Service.AdjustAsync(card.Id, -25m, "key", null, "correction")).Success);
        var result = await fixture.Service.RedeemAsync(card.PublicCode, 25m, "key", null, null);
        Assert.Equal(GiftCardFailure.IdempotencyConflict, result.Failure);
        Assert.Single(await fixture.Db.GiftCardTransactions.ToListAsync());
    }

    [Fact]
    public async Task Replay_after_another_redemption_returns_original_amount_and_current_balance()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.RedeemAsync("GC-AAAA-BBBB-CCCC", 25m, "first", null, null);
        await fixture.Service.RedeemAsync("GC-AAAA-BBBB-CCCC", 75m, "second", null, null);
        var replay = await fixture.Service.RedeemAsync("GC-AAAA-BBBB-CCCC", 25m, "first", null, null);
        Assert.True(replay.WasIdempotent);
        Assert.Equal(25m, replay.RedeemedAmount);
        Assert.Equal(0m, replay.Detail!.Card.CurrentBalance);
        Assert.Equal(GiftCardStatus.FullyRedeemed, replay.Detail.Card.Status);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Write_conflict_rechecks_committed_operation_and_discards_failed_changes(bool sameKey, bool concurrency)
    {
        var race = new WinningWriteInterceptor(sameKey, concurrency);
        await using var fixture = await Fixture.CreateAsync(race);
        race.Armed = true;
        var result = await fixture.Service.RedeemAsync("GC-AAAA-BBBB-CCCC", 25m, "key", null, null);
        Assert.Equal(sameKey, result.Success);
        Assert.Equal(sameKey, result.WasIdempotent);
        if (!sameKey) Assert.Equal(GiftCardFailure.ConcurrencyConflict, result.Failure);
        Assert.False(fixture.Db.ChangeTracker.HasChanges());
        Assert.Single(await fixture.Db.GiftCardTransactions.AsNoTracking().ToListAsync());
        Assert.Equal(75m, (await fixture.Db.GiftCards.AsNoTracking().SingleAsync(x => x.PublicCode == "GC-AAAA-BBBB-CCCC")).CurrentBalance);
    }

    // Deterministically exercises recovery branches, not SQL row-version enforcement.
    private sealed class WinningWriteInterceptor(bool sameKey, bool concurrency) : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public Func<AppDbContext> CreateContext { get; set; } = null!;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Armed) return result;
            Armed = false;
            var pending = eventData.Context!.ChangeTracker.Entries<GiftCardTransaction>().Single().Entity;
            await using var winner = CreateContext();
            var card = await winner.GiftCards.SingleAsync(x => x.Id == pending.GiftCardId, cancellationToken);
            var balances = card.Redeem(25m, true, DateTime.UtcNow);
            winner.Add(new GiftCardTransaction(Guid.NewGuid(), card.TenantId, card.Id,
                GiftCardTransactionType.Redeemed, -25m, balances.Before, balances.After,
                pending.PerformedByUserId, DateTime.UtcNow, idempotencyKey: sameKey ? "key" : "other"));
            await winner.SaveChangesAsync(cancellationToken);
            if (concurrency) throw new DbUpdateConcurrencyException("Simulated losing writer.");
            throw new DbUpdateException("Simulated unique-key race.");
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required AppDbContext Db { get; init; }
        public required GiftCardService Service { get; init; }
        public static async Task<Fixture> CreateAsync(WinningWriteInterceptor? interceptor = null)
        {
            var tenantId = Guid.NewGuid(); var userId = Guid.NewGuid(); var now = DateTime.UtcNow;
            var tenant = new Mock<ITenantContext>(); tenant.SetupGet(x => x.TenantId).Returns(tenantId); tenant.SetupGet(x => x.HasTenant).Returns(true);
            var user = new Mock<ICurrentUserService>(); user.SetupGet(x => x.UserId).Returns(userId.ToString());
            var clock = new Mock<IDateTimeProvider>(); clock.SetupGet(x => x.UtcNow).Returns(now);
            var databaseName = Guid.NewGuid().ToString();
            var databaseRoot = new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot();
            var plainOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName, databaseRoot).Options;
            var options = new DbContextOptionsBuilder<AppDbContext>(plainOptions);
            if (interceptor is not null)
            {
                interceptor.CreateContext = () => new AppDbContext(plainOptions, new Mock<IPublisher>().Object, tenant.Object);
                options.AddInterceptors(interceptor);
            }
            var db = new AppDbContext(options.Options, new Mock<IPublisher>().Object, tenant.Object);
            var config = new GiftCardConfiguration(Guid.NewGuid(), tenantId, now);
            config.Update(true, true, true, true, GiftCardExpirationMode.Never, null, "MXN", "Gift Card", "#312ee9", "#FFFFFF", null, null, null, null, now);
            db.Add(config);
            foreach (var code in new[] { "GC-AAAA-BBBB-CCCC", "GC-DDDD-EEEE-FFFF" })
                db.Add(new GiftCard(Guid.NewGuid(), tenantId, code, GiftCard.HashClaimToken(code), 100m, "MXN", null, "Test", null, null, null, null, GiftCardSource.Manual, userId, now, null));
            await db.SaveChangesAsync();
            return new Fixture { Db = db, Service = new GiftCardService(db, new Mock<IDbContextFactory<AppDbContext>>().Object,
                tenant.Object, clock.Object, user.Object, new Mock<IGiftCardWalletService>().Object, new Mock<IGiftCardAppleWalletService>().Object) };
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
