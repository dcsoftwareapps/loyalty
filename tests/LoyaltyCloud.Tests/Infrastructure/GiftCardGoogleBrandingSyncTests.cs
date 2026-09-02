using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GiftCardGoogleBrandingSyncTests
{
    [Fact]
    public async Task SynchronizeBranding_updates_only_current_tenant_google_objects_and_preserves_financial_data()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var cardA = Card(tenantA, userId, "GC-AAAA-BBBB-CCCC", 250m, now);
        var cardB = Card(tenantB, userId, "GC-DDDD-EEEE-FFFF", 400m, now);
        await using (var seedA = Context(options, tenantA))
        {
            var configA = Configuration(tenantA, "Tamalitos", "#123456", "https://assets.test/tamalitos.png", now);
            seedA.AddRange(configA, cardA,
                new GiftCardWallet(Guid.NewGuid(), tenantA, cardA.Id, GiftCardWalletProvider.Google, "issuer.giftcard_a", "issuer.object_a", now),
                new GiftCardWallet(Guid.NewGuid(), tenantA, cardA.Id, GiftCardWalletProvider.Apple, "apple.class_a", "apple.object_a", now),
                new GiftCardTransaction(Guid.NewGuid(), tenantA, cardA.Id, GiftCardTransactionType.Issued, 250m, 0m, 250m, userId, now));
            await seedA.SaveChangesAsync();
        }
        await using (var seedB = Context(options, tenantB))
        {
            var configB = Configuration(tenantB, "KBeauty", "#654321", "https://assets.test/kbeauty.png", now);
            seedB.AddRange(configB, cardB,
                new GiftCardWallet(Guid.NewGuid(), tenantB, cardB.Id, GiftCardWalletProvider.Google, "issuer.giftcard_b", "issuer.object_b", now));
            await seedB.SaveChangesAsync();
        }

        var google = new Mock<IGoogleWalletClient>();
        await using var db = Context(options, tenantA);
        var service = Service(db, options, tenantA, now, google.Object);

        var result = await service.SynchronizeBrandingAsync();

        Assert.Equal(new(1, 0), result);
        google.Verify(x => x.EnsureGiftCardClassAsync(
            It.Is<GoogleGiftCardClassData>(c => c.Id == "issuer.giftcard_a" && c.IssuerName == "Tamalitos"),
            It.IsAny<CancellationToken>()), Times.Once);
        google.Verify(x => x.CreateOrUpdateGiftCardObjectAsync(
            It.Is<GoogleGiftCardObjectData>(o =>
                o.Id == "issuer.object_a" && o.ClassId == "issuer.giftcard_a" &&
                o.HexBackgroundColor == "#123456" &&
                o.LogoUri == "https://assets.test/tamalitos.png" &&
                o.HeroImageUri == "https://assets.test/tamalitos.png"),
            It.IsAny<CancellationToken>()), Times.Once);
        google.Verify(x => x.CreateOrUpdateGiftCardObjectAsync(
            It.Is<GoogleGiftCardObjectData>(o => o.Id == "issuer.object_b"),
            It.IsAny<CancellationToken>()), Times.Never);

        await using var verify = Context(options, tenantA);
        Assert.Equal(250m, (await verify.GiftCards.SingleAsync()).CurrentBalance);
        Assert.Single(await verify.GiftCardTransactions.ToListAsync());
        Assert.Equal(2, await verify.GiftCardWallets.CountAsync());
    }

    [Fact]
    public async Task SynchronizeBranding_reports_provider_failure_without_mutating_balance_or_ledger()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var card = Card(tenantId, userId, "GC-AAAA-BBBB-CCCC", 250m, now);
        await using (var seed = Context(options, tenantId))
        {
            seed.AddRange(Configuration(tenantId, "Tamalitos", "#123456", null, now), card,
                new GiftCardWallet(Guid.NewGuid(), tenantId, card.Id, GiftCardWalletProvider.Google, "issuer.giftcard", "issuer.object", now),
                new GiftCardTransaction(Guid.NewGuid(), tenantId, card.Id, GiftCardTransactionType.Issued, 250m, 0m, 250m, userId, now));
            await seed.SaveChangesAsync();
        }
        var google = new Mock<IGoogleWalletClient>();
        google.Setup(x => x.CreateOrUpdateGiftCardObjectAsync(It.IsAny<GoogleGiftCardObjectData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("provider unavailable"));
        await using var db = Context(options, tenantId);

        var result = await Service(db, options, tenantId, now, google.Object).SynchronizeBrandingAsync();

        Assert.Equal(new(1, 1), result);
        await using var verify = Context(options, tenantId);
        Assert.Equal(250m, (await verify.GiftCards.SingleAsync()).CurrentBalance);
        Assert.Single(await verify.GiftCardTransactions.ToListAsync());
    }

    private static GiftCardWalletService Service(AppDbContext db, DbContextOptions<AppDbContext> options, Guid tenantId, DateTime now, IGoogleWalletClient google)
    {
        var tenant = Tenant(tenantId);
        var clock = new Mock<IDateTimeProvider>(); clock.SetupGet(x => x.UtcNow).Returns(now);
        var walletOptions = Options.Create(new GoogleWalletOptions { Enabled = true, IssuerId = "issuer" });
        return new(db, new TestDbContextFactory(() => Context(options, tenantId)), tenant.Object, google,
            new Mock<IGoogleWalletCredentialsProvider>().Object, new GoogleWalletJwtFactory(walletOptions), walletOptions,
            clock.Object, NullLogger<GiftCardWalletService>.Instance);
    }

    private static GiftCardConfiguration Configuration(Guid tenantId, string name, string color, string? logo, DateTime now)
    {
        var config = new GiftCardConfiguration(Guid.NewGuid(), tenantId, now);
        config.Update(true, true, true, false, GiftCardExpirationMode.Never, null, "MXN", name, color, "#FFFFFF", logo, null, null, null, now);
        return config;
    }

    private static GiftCard Card(Guid tenantId, Guid userId, string code, decimal value, DateTime now) =>
        new(Guid.NewGuid(), tenantId, code, GiftCard.HashClaimToken(Guid.NewGuid().ToString()), value, "MXN", null,
            "Owner", null, null, null, null, GiftCardSource.Manual, userId, now, null);

    private static AppDbContext Context(DbContextOptions<AppDbContext> options, Guid tenantId) =>
        new(options, new Mock<IPublisher>().Object, Tenant(tenantId).Object);

    private static Mock<ITenantContext> Tenant(Guid tenantId)
    {
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(x => x.TenantId).Returns(tenantId);
        tenant.SetupGet(x => x.HasTenant).Returns(true);
        return tenant;
    }

    private sealed class TestDbContextFactory(Func<AppDbContext> create) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => create();
    }
}
