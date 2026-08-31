extern alias AdminApp;

using System.Security.Claims;
using AdminApp::LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class GiftCardFeatureToggleTests
{
    [Fact]
    public void Sidebar_ShowsGiftCardsOnlyInsideEnabledConditional()
    {
        var source = Read("src", "LoyaltyCloud.Admin", "Components", "Layout", "MainLayout.razor");
        Assert.Contains("@if (giftCardsEnabled)", source);
        Assert.Contains("<span class=\"kb-sidebar-section\">Gift Cards</span>", source);
        Assert.Contains("giftCardsEnabled = await GiftCards.IsEnabledAsync()", source);
    }

    [Fact]
    public void GeneralConfiguration_AlwaysContainsOnlyTheBootstrapToggle()
    {
        var source = Read("src", "LoyaltyCloud.Admin", "Pages", "Config.razor");
        Assert.Contains("Habilitar Gift Cards", source);
        Assert.Contains("GiftCards.SetEnabledAsync(giftCardsEnabled)", source);
        Assert.DoesNotContain("AllowPartialRedemption", source);
        Assert.DoesNotContain("GiftCardDenomination", source);
    }

    [Fact]
    public async Task AuthorizationPolicy_DeniesOffAndAllowsOn()
    {
        var service = new Mock<IGiftCardService>();
        var requirement = new GiftCardsEnabledRequirement();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "owner")], "test"));

        service.Setup(x => x.IsEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var denied = new AuthorizationHandlerContext([requirement], principal, null);
        await new GiftCardsEnabledHandler(service.Object).HandleAsync(denied);
        Assert.False(denied.HasSucceeded);

        service.Setup(x => x.IsEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var allowed = new AuthorizationHandlerContext([requirement], principal, null);
        await new GiftCardsEnabledHandler(service.Object).HandleAsync(allowed);
        Assert.True(allowed.HasSucceeded);
    }

    [Fact]
    public void EveryAdministrativeGiftCardRoute_UsesFeaturePolicy()
    {
        var pages = new[] { "GiftCards.razor", "GiftCardIssue.razor", "GiftCardRedeem.razor", "GiftCardList.razor", "GiftCardDetail.razor", "GiftCardReports.razor", "GiftCardSettings.razor" };
        foreach (var page in pages)
            Assert.Contains("Authorize(Policy = LoyaltyCloud.Admin.Auth.GiftCardsAuthorization.Policy)", Read("src", "LoyaltyCloud.Admin", "Pages", page));
    }

    [Fact]
    public async Task EnablingTenantA_DoesNotEnableTenantB_AndDisablingPreservesData()
    {
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid(); var now = DateTime.UtcNow; var user = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using var tenantAContext = Context(options, tenantA);
        var config = new GiftCardConfiguration(Guid.NewGuid(), tenantA, now);
        var card = new GiftCard(Guid.NewGuid(), tenantA, "GC-AAAA-BBBB-CCCC", GiftCard.HashClaimToken("claim"), 500m, "MXN", null, "Owner", null, null, null, null, GiftCardSource.Manual, user, now, null);
        tenantAContext.AddRange(config, card, new GiftCardTransaction(Guid.NewGuid(), tenantA, card.Id, GiftCardTransactionType.Issued, 500m, 0, 500m, user, now));
        await tenantAContext.SaveChangesAsync();
        var serviceA = Service(tenantAContext, tenantA, user, now);
        await serviceA.SetEnabledAsync(true);
        Assert.True(await serviceA.IsEnabledAsync());

        await using var tenantBContext = Context(options, tenantB);
        var serviceB = Service(tenantBContext, tenantB, user, now);
        Assert.False(await serviceB.IsEnabledAsync());

        await serviceA.SetEnabledAsync(false);
        Assert.False(await serviceA.IsEnabledAsync());
        Assert.Single(await tenantAContext.GiftCards.ToListAsync());
        Assert.Single(await tenantAContext.GiftCardTransactions.ToListAsync());
        Assert.Empty(await tenantBContext.GiftCards.ToListAsync());
    }

    private static GiftCardService Service(AppDbContext db, Guid tenantId, Guid userId, DateTime now)
    {
        var tenant = new Mock<ITenantContext>(); tenant.SetupGet(x => x.TenantId).Returns(tenantId); tenant.SetupGet(x => x.HasTenant).Returns(true);
        var clock = new Mock<IDateTimeProvider>(); clock.SetupGet(x => x.UtcNow).Returns(now); clock.SetupGet(x => x.Today).Returns(now.Date);
        var user = new Mock<ICurrentUserService>(); user.SetupGet(x => x.UserId).Returns(userId.ToString());
        return new GiftCardService(db, tenant.Object, clock.Object, user.Object, new Mock<IGiftCardWalletService>().Object, new Mock<IGiftCardAppleWalletService>().Object);
    }
    private static AppDbContext Context(DbContextOptions<AppDbContext> options, Guid tenantId)
    {
        var tenant = new Mock<ITenantContext>(); tenant.SetupGet(x => x.TenantId).Returns(tenantId); tenant.SetupGet(x => x.HasTenant).Returns(true);
        return new AppDbContext(options, new Mock<IPublisher>().Object, tenant.Object);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(GetRoot(), Path.Combine(parts)));
    private static string GetRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoyaltyCloud.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
