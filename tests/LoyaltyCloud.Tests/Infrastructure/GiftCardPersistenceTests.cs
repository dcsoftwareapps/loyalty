using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GiftCardPersistenceTests
{
    private static readonly Guid TenantId = Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("91000000-0000-0000-0000-000000000002");
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

    [Fact]
    public async Task RotateClaimToken_ReplacesHashAndPreservesBalanceAndHistory()
    {
        await using var database = await TestDatabase.CreateAsync(300m, "recipient@example.test");
        await using var db = database.Context();
        var service = Service(db);

        var issued = await service.RotateClaimTokenAsync((await db.GiftCards.AsNoTracking().SingleAsync()).Id);

        Assert.NotEqual("claim", issued.ClaimToken);
        Assert.NotEqual(GiftCard.HashClaimToken("claim"), (await db.GiftCards.AsNoTracking().SingleAsync()).ClaimTokenHash);
        Assert.Equal(GiftCard.HashClaimToken(issued.ClaimToken), (await db.GiftCards.AsNoTracking().SingleAsync()).ClaimTokenHash);
        Assert.Equal(300m, issued.Card.CurrentBalance);
        Assert.Equal(1, await db.GiftCardTransactions.CountAsync());

        await using var claimDb = database.ContextWithoutTenant();
        var tenantContext = new TestMutableTenantContext();
        var claimService = new GiftCardClaimService(
            claimDb,
            Clock().Object,
            tenantContext,
            Branding(),
            new Mock<IGiftCardWalletService>().Object,
            new Mock<IGiftCardAppleWalletService>().Object);

        Assert.Null(await claimService.GetAsync("claim"));
        var claim = await claimService.GetAsync(issued.ClaimToken);
        Assert.NotNull(claim);
        Assert.Equal(TenantId, tenantContext.TenantId);

        var sender = new RecordingSender();
        var delivery = new GiftCardDeliveryService(sender, new EmailConfiguration(), NullLogger<GiftCardDeliveryService>.Instance);
        var result = await delivery.SendEmailAsync(issued, issued.Card.RecipientEmail!, "Gift Card Test");
        var email = Assert.Single(sender.Messages);
        Assert.Equal(GiftCardDeliveryStatus.Sent, result.Status);
        Assert.Contains($"/giftcards/claim/{issued.ClaimToken}", email.TextBody);
        Assert.DoesNotContain("/giftcards/claim/claim", email.TextBody);
        Assert.Equal("recipient@example.test", email.Recipient);
    }

    [Fact]
    public async Task RotateClaimToken_RequiresRecipientEmail()
    {
        await using var database = await TestDatabase.CreateAsync(300m);
        await using var db = database.Context();
        var service = Service(db);

        var cardId = (await db.GiftCards.AsNoTracking().SingleAsync()).Id;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RotateClaimTokenAsync(cardId));
    }

    [Fact]
    public async Task RotateClaimToken_CannotAccessAnotherTenantGiftCard()
    {
        await using var database = await TestDatabase.CreateAsync(300m, "recipient@example.test");
        await using var db = database.Context();
        var cardId = (await db.GiftCards.AsNoTracking().SingleAsync()).Id;
        await database.AddTenantConfigurationAsync(OtherTenantId);
        await using var otherTenantDb = database.Context(OtherTenantId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(otherTenantDb, OtherTenantId).RotateClaimTokenAsync(cardId));
    }

    [Fact]
    public async Task EmailFailureAfterIssue_DoesNotUndoGiftCardIssuance()
    {
        await using var database = await TestDatabase.CreateEmptyAsync();
        await using var db = database.Context();
        var service = Service(db);

        var issued = await service.IssueAsync(new(500m, null, "Cliente", "recipient@example.test", null, "Daniel", "Felicidades"));

        Assert.Equal(500m, issued.Card.CurrentBalance);
        Assert.Single(await db.GiftCards.AsNoTracking().ToListAsync());
        Assert.Single(await db.GiftCardTransactions.AsNoTracking().ToListAsync());
    }

    private static GiftCardService Service(AppDbContext db, Guid? tenantId = null)
    {
        var clock = Clock();
        var user = new Mock<ICurrentUserService>(); user.SetupGet(x => x.UserId).Returns(UserId.ToString());
        var google = new Mock<IGiftCardWalletService>(); google.Setup(x => x.SynchronizeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var apple = new Mock<IGiftCardAppleWalletService>(); apple.Setup(x => x.SynchronizeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var tenant = new Mock<ITenantContext>(); tenant.SetupGet(x => x.TenantId).Returns(tenantId ?? TenantId); tenant.SetupGet(x => x.HasTenant).Returns(true);
        return new GiftCardService(db, new Mock<IDbContextFactory<AppDbContext>>().Object, tenant.Object, clock.Object, user.Object, google.Object, apple.Object);
    }

    private static Mock<IDateTimeProvider> Clock()
    {
        var clock = new Mock<IDateTimeProvider>(); clock.SetupGet(x => x.UtcNow).Returns(Now); clock.SetupGet(x => x.Today).Returns(Now.Date);
        return clock;
    }

    private static ITenantBrandingReadService Branding()
    {
        var branding = new Mock<ITenantBrandingReadService>();
        branding.Setup(x => x.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantBrandingInfo(TenantId, "giftcard-test", "Gift Card Test", "#111111", "#FFFFFF", null, null, "#111111", null, false, 100, "CustomerName", null, false, null, null, null, null));
        return branding.Object;
    }

    private sealed class TestMutableTenantContext : IMutableTenantContext
    {
        public Guid? TenantId { get; private set; }
        public string? TenantSlug { get; private set; }
        public bool HasTenant => TenantId is not null;
        public void SetTenant(Guid tenantId, string tenantSlug) { TenantId = tenantId; TenantSlug = tenantSlug; }
        public void Clear() { TenantId = null; TenantSlug = null; }
    }

    private sealed class EmailConfiguration : IBillingEmailConfigurationProvider
    {
        public Task<BillingEmailSettingsDto> GetAsync(CancellationToken ct = default) => Task.FromResult(
            new BillingEmailSettingsDto(true, "SMTP", "notifications@example.test", "LoyaltyCloud", "https://admin.example.test", true, true));
    }

    private sealed class RecordingSender : ITransactionalEmailSender
    {
        public List<TransactionalEmail> Messages { get; } = [];
        public Task SendAsync(TransactionalEmail email, CancellationToken ct = default)
        {
            Messages.Add(email);
            return Task.CompletedTask;
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private TestDatabase(DbContextOptions<AppDbContext> options) => _options = options;

        public static async Task<TestDatabase> CreateAsync(decimal balance, string? recipientEmail = null)
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
                "Cliente", recipientEmail, null, null, null, GiftCardSource.Manual, UserId, Now, null);
            db.Add(config); db.Add(card);
            db.Add(new GiftCardTransaction(Guid.NewGuid(), TenantId, card.Id, GiftCardTransactionType.Issued, balance, 0, balance, UserId, Now));
            await db.SaveChangesAsync();
            return result;
        }

        public static async Task<TestDatabase> CreateEmptyAsync()
        {
            var result = await CreateAsync(1m, "seed@example.test");
            await using var db = result.Context();
            db.GiftCardTransactions.RemoveRange(db.GiftCardTransactions);
            db.GiftCards.RemoveRange(db.GiftCards);
            await db.SaveChangesAsync();
            return result;
        }

        public async Task AddTenantConfigurationAsync(Guid tenantId)
        {
            await using var db = Context(tenantId);
            db.Tenants.Add(new Tenant(tenantId, $"tenant-{tenantId:N}"[..20], "Other Tenant", "UTC", Now));
            db.TenantSubscriptions.Add(new TenantSubscription(tenantId, TenantSubscriptionStatus.Active, "test", paidThroughUtc: Now.AddYears(1)));
            var config = new GiftCardConfiguration(Guid.NewGuid(), tenantId, Now);
            config.Update(true, true, true, true, GiftCardExpirationMode.Never, null, "MXN", "Gift Card", "#111111", "#FFFFFF", null, null, null, null, Now);
            db.GiftCardConfigurations.Add(config);
            await db.SaveChangesAsync();
        }

        public AppDbContext Context(Guid? tenantId = null)
        {
            var tenant = new Mock<ITenantContext>(); tenant.SetupGet(x => x.TenantId).Returns(tenantId ?? TenantId); tenant.SetupGet(x => x.HasTenant).Returns(true);
            return new AppDbContext(_options, new Mock<IPublisher>().Object, tenant.Object);
        }

        public AppDbContext ContextWithoutTenant()
        {
            var tenant = new Mock<ITenantContext>();
            return new AppDbContext(_options, new Mock<IPublisher>().Object, tenant.Object);
        }

        public async ValueTask DisposeAsync()
        {
            await using var db = Context();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
