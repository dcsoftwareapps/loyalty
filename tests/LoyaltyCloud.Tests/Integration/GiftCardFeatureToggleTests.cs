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
        Assert.Equal(1, source.Split("<span>Gift Cards</span>", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("<span>Resumen</span>", source);
        Assert.Contains("Match=\"NavLinkMatch.Prefix\"", source);
        Assert.Contains("giftCardsEnabled = await GiftCards.IsEnabledAsync()", source);
    }

    [Fact]
    public void CircuitLayoutReads_CreateIndependentDbContexts()
    {
        var authSource = Read("src", "LoyaltyCloud.Admin", "Auth", "AdminAuthService.cs");
        var giftCardSource = Read("src", "LoyaltyCloud.Infrastructure", "Services", "GiftCardService.cs");

        Assert.Contains("IDbContextFactory<AppDbContext>", authSource);
        Assert.Contains("_dbContextFactory.CreateDbContextAsync(ct)", authSource);
        Assert.Contains("dbContextFactory.CreateDbContextAsync(ct)", giftCardSource);
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
    public void GiftCardUi_PreservesCustomerDeliveryAndRemovesAdminCreditEntryPoints()
    {
        var detail = Read("src", "LoyaltyCloud.Admin", "Pages", "GiftCardDetail.razor");
        var claim = Read("src", "LoyaltyCloud.Admin", "Pages", "GiftCardClaim.razor");
        Assert.DoesNotContain("AdjustAsync", detail);
        Assert.DoesNotContain("Agregar a Apple Wallet", detail);
        Assert.DoesNotContain("Agregar a Google Wallet", detail);
        Assert.Contains("RedeemAsync", detail);
        Assert.Contains("Confirmar canje", detail);
        Assert.Contains("Cancelar Gift Card", detail);
        Assert.Contains("Agregar a Apple Wallet", claim);
        Assert.Contains("Agregar a Google Wallet", claim);
    }

    [Fact]
    public void Scanner_DisposalGuardsInteropUntilCameraActuallyStarted()
    {
        var source = Read("src", "LoyaltyCloud.Admin", "Components", "GiftCardQrScanner.razor");
        Assert.Contains("if(!jsStarted||disposed)return", source);
        Assert.Contains("if(disposed)return;await StopAsync()", source);
        Assert.Contains("catch(JSDisconnectedException)", source);
        Assert.Contains("catch(ObjectDisposedException)", source);
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
        var serviceA = Service(tenantAContext, options, tenantA, user, now);
        await serviceA.SetEnabledAsync(true);
        Assert.True(await serviceA.IsEnabledAsync());

        await using var tenantBContext = Context(options, tenantB);
        var serviceB = Service(tenantBContext, options, tenantB, user, now);
        Assert.False(await serviceB.IsEnabledAsync());

        await serviceA.SetEnabledAsync(false);
        Assert.False(await serviceA.IsEnabledAsync());
        Assert.Single(await tenantAContext.GiftCards.ToListAsync());
        Assert.Single(await tenantAContext.GiftCardTransactions.ToListAsync());
        Assert.Empty(await tenantBContext.GiftCards.ToListAsync());
    }

    private static GiftCardService Service(AppDbContext db, DbContextOptions<AppDbContext> options, Guid tenantId, Guid userId, DateTime now)
    {
        var tenant = new Mock<ITenantContext>(); tenant.SetupGet(x => x.TenantId).Returns(tenantId); tenant.SetupGet(x => x.HasTenant).Returns(true);
        var clock = new Mock<IDateTimeProvider>(); clock.SetupGet(x => x.UtcNow).Returns(now); clock.SetupGet(x => x.Today).Returns(now.Date);
        var user = new Mock<ICurrentUserService>(); user.SetupGet(x => x.UserId).Returns(userId.ToString());
        var factory = new TestDbContextFactory(() => Context(options, tenantId));
        return new GiftCardService(db, factory, tenant.Object, clock.Object, user.Object, new Mock<IGiftCardWalletService>().Object, new Mock<IGiftCardAppleWalletService>().Object);
    }
    private sealed class TestDbContextFactory(Func<AppDbContext> create) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => create();
    }

    private static AppDbContext Context(DbContextOptions<AppDbContext> options, Guid tenantId)
    {
        var tenant = new Mock<ITenantContext>(); tenant.SetupGet(x => x.TenantId).Returns(tenantId); tenant.SetupGet(x => x.HasTenant).Returns(true);
        return new AppDbContext(options, new Mock<IPublisher>().Object, tenant.Object);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(GetRoot(), Path.Combine(parts)));
    private static string GetRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("LOYALTY_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot) && File.Exists(Path.Combine(configuredRoot, "LoyaltyCloud.sln"))) return configuredRoot;
        if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "LoyaltyCloud.sln"))) return Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoyaltyCloud.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
