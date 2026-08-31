using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GiftCardTenantIsolationTests
{
    [Fact]
    public async Task QueryFilters_IsolateCardsTransactionsSettingsAndWallets()
    {
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid(); var user = Guid.NewGuid(); var now = DateTime.UtcNow;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var db = Context(options, tenantA))
        {
            var config = new GiftCardConfiguration(Guid.NewGuid(), tenantA, now); config.Update(true,true,true,true,GiftCardExpirationMode.Never,null,"MXN","A","#111111","#FFFFFF",null,null,null,null,now);
            var card = new GiftCard(Guid.NewGuid(),tenantA,"GC-AAAA-BBBB-CCCC",GiftCard.HashClaimToken("token-a"),100,"MXN",null,"A",null,null,null,null,GiftCardSource.Manual,user,now,null);
            db.Add(config); db.Add(card); db.Add(new GiftCardTransaction(Guid.NewGuid(),tenantA,card.Id,GiftCardTransactionType.Issued,100,0,100,user,now)); db.Add(new GiftCardWallet(Guid.NewGuid(),tenantA,card.Id,GiftCardWalletProvider.Google,"class-a","object-a",now));
            await db.SaveChangesAsync();
        }
        await using (var db = Context(options, tenantB))
        {
            Assert.Empty(await db.GiftCards.ToListAsync());
            Assert.Empty(await db.GiftCardTransactions.ToListAsync());
            Assert.Empty(await db.GiftCardConfigurations.ToListAsync());
            Assert.Empty(await db.GiftCardWallets.ToListAsync());
        }
    }

    [Fact]
    public async Task SaveGuard_RejectsCrossTenantGiftCard()
    {
        var tenantA=Guid.NewGuid(); var tenantB=Guid.NewGuid();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db=Context(options,tenantB);
        db.GiftCards.Add(new GiftCard(Guid.NewGuid(),tenantA,"GC-AAAA-BBBB-DDDD",GiftCard.HashClaimToken("token"),10,"MXN",null,"A",null,null,null,null,GiftCardSource.Manual,Guid.NewGuid(),DateTime.UtcNow,null));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
    }

    private static AppDbContext Context(DbContextOptions<AppDbContext> options, Guid tenantId)
    {
        var tenant=new Mock<ITenantContext>(); tenant.SetupGet(x=>x.TenantId).Returns(tenantId); tenant.SetupGet(x=>x.HasTenant).Returns(true);
        return new AppDbContext(options,new Mock<IPublisher>().Object,tenant.Object);
    }
}
