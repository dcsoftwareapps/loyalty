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
        Assert.Equal(1, source.Split("<span>Tarjetas de regalo</span>", StringSplitOptions.None).Length - 1);
        Assert.Contains("<span class=\"kb-sidebar-section\">Tarjetas de regalo</span>", source);
        Assert.Contains("<span>Resumen</span>", source);
        Assert.Contains("<span>Emitir</span>", source);
        Assert.Contains("<span>Consultar y canjear</span>", source);
        Assert.Contains("<span>Tarjetas</span>", source);
        Assert.Contains("href=\"/giftcards/reports\"", source);
        Assert.DoesNotContain("href=\"/giftcards/settings\"", source);
        Assert.Contains("giftCardsEnabled = await GiftCards.IsEnabledAsync()", source);
    }

    [Fact]
    public void Sidebar_OrderAndFeatureEntriesMatchFinalInformationArchitecture()
    {
        var source = Read("src", "LoyaltyCloud.Admin", "Components", "Layout", "MainLayout.razor");
        var principal = source.IndexOf(">Principal</span>", StringComparison.Ordinal);
        var programa = source.IndexOf(">Programa</span>", StringComparison.Ordinal);
        var giftCards = source.IndexOf(">Tarjetas de regalo</span>", StringComparison.Ordinal);
        var reportes = source.IndexOf(">Reportes</span>", StringComparison.Ordinal);
        var gestion = source.IndexOf(">Gestión</span>", StringComparison.Ordinal);
        Assert.True(principal < programa && programa < giftCards && giftCards < reportes && reportes < gestion);
        Assert.Contains("GiftCardFeatureState.Changed", source);
    }

    [Fact]
    public void DisabledFeatureUsesAuthenticatedRedirectInsteadOfLoginRedirect()
    {
        var routes = Read("src", "LoyaltyCloud.Admin", "Routes.razor");
        var redirect = Read("src", "LoyaltyCloud.Admin", "Components", "RedirectUnauthorized.razor");
        Assert.Contains("RedirectUnauthorized", routes);
        Assert.Contains("IsAuthenticated == true", redirect);
        Assert.Contains("NavigateTo(\"/dashboard\")", redirect);
    }

    [Fact]
    public void IssuePageSupportsDenominationsAndUnambiguousCustomAmount()
    {
        var source = Read("src", "LoyaltyCloud.Admin", "Pages", "GiftCardIssue.razor");
        Assert.Contains("activeDenominations", source);
        Assert.Contains("SelectDenomination", source);
        Assert.Contains("selectedDenomination", source);
        Assert.Contains("CustomAmountChanged", source);
        Assert.Contains("settings.AllowCustomAmount", source);
        Assert.Contains("selectZero(this)", source);
        Assert.DoesNotContain("model.Phone", source);
    }

    [Fact]
    public void ClaimUsesDirectWalletNavigationAndCorrectAppleMimeType()
    {
        var claim = Read("src", "LoyaltyCloud.Admin", "Pages", "GiftCardClaim.razor");
        var program = Read("src", "LoyaltyCloud.Admin", "Program.cs");
        Assert.Contains("href=\"@WalletUrl\"", claim);
        Assert.Contains("/wallet/apple", claim);
        Assert.Contains("/wallet/google", claim);
        Assert.Contains("application/vnd.apple.pkpass", program);
        Assert.Contains("Results.Redirect(link.Url)", program);
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
    public void GeneralConfiguration_UnifiesLoyaltyAndGiftCardDesigners()
    {
        var source = Read("src", "LoyaltyCloud.Admin", "Pages", "Config.razor");
        var panel = Read("src", "LoyaltyCloud.Admin", "Components", "GiftCardSettingsPanel.razor");
        Assert.Contains("GiftCardSettingsPanel", source);
        Assert.Contains("section=giftcards", source);
        Assert.Contains(">Tarjeta digital</a>", source);
        Assert.Contains(">Tarjetas de regalo</a>", source);
        Assert.Contains(">Puntos y beneficios</a>", source);
        Assert.Contains("section=giftcards", source);
        Assert.Contains("section=points", source);
        Assert.Contains("kb-report-nav", source);
        Assert.DoesNotContain("kb-config-tabs", source);
        Assert.Contains("Tarjetas de regalo habilitadas", panel);
        Assert.Contains("AllowPartialRedemption", panel);
        Assert.Contains("GiftCardVisual", panel);
        Assert.DoesNotContain("<span>Moneda</span>", panel);
    }

    [Fact]
    public void GiftCardConfigurationPreview_MatchesAppleGiftCardFrontPresentation()
    {
        var panel = Read("src", "LoyaltyCloud.Admin", "Components", "GiftCardSettingsPanel.razor");
        var visual = Read("src", "LoyaltyCloud.Admin", "Components", "GiftCardVisual.razor");
        var css = Read("src", "LoyaltyCloud.Admin", "wwwroot", "css", "site.css");

        Assert.Contains("DisplayName=\"@PreviewBranding.DisplayName\"", panel);
        Assert.Contains("BackgroundColor=\"@PreviewBranding.BackgroundColor\"", panel);
        Assert.Contains("TextColor=\"@PreviewBranding.TextColor\"", panel);
        Assert.Contains("LogoUrl=\"@PreviewLogoUrl\"", panel);
        Assert.Contains("SenderName=\"Loyalty\"", panel);
        Assert.Contains("Balance=\"$200\"", panel);
        Assert.Contains("ShowCategoryLabel=\"false\"", panel);
        Assert.Contains("ShowSecondaryText=\"false\"", panel);
        Assert.Contains("ShowRecipient=\"false\"", panel);
        Assert.Contains("ShowBalanceLabel=\"false\"", panel);
        Assert.Contains("ShowCurrency=\"false\"", panel);
        Assert.Contains("ShowExpiration=\"false\"", panel);
        Assert.DoesNotContain("SecondaryText=\"@", panel);
        Assert.DoesNotContain("Balance=\"1,000.00\"", panel);
        Assert.DoesNotContain("Expiration=\"Sin expiración\"", panel);

        Assert.Contains("@if (ShowCategoryLabel)", visual);
        Assert.Contains("@if (ShowSecondaryText", visual);
        Assert.Contains("kb-gift-card-visual__sender", visual);
        Assert.Contains("@if (ShowRecipient", visual);
        Assert.Contains("@if (ShowBalanceLabel)", visual);
        Assert.True(visual.IndexOf("kb-gift-card-visual__balance", StringComparison.Ordinal) < visual.IndexOf("kb-gift-card-visual__sender", StringComparison.Ordinal));
        Assert.Contains("kb-gift-card-visual__meta", visual);
        Assert.Contains("VÁLIDA HASTA", visual);
        Assert.Contains("aspect-ratio:.72/1", css);
        Assert.Contains("min-height:420px", css);
    }

    [Fact]
    public void GiftCardSettingsPanel_UsesSimplifiedBrandingFormAndPreservesHiddenValues()
    {
        var panel = Read("src", "LoyaltyCloud.Admin", "Components", "GiftCardSettingsPanel.razor");

        Assert.Contains("<span>Título de la tarjeta</span>", panel);
        Assert.Contains("<span>Color de la tarjeta</span>", panel);
        Assert.Contains("<span>Color de texto</span>", panel);
        Assert.Contains("<span>Logo de la tarjeta</span>", panel);
        Assert.Contains("UploadGiftCardLogoAsync", panel);
        Assert.Contains("class=\"kb-file-upload\"", panel);
        Assert.Contains("class=\"kb-file-input-hidden\"", panel);
        Assert.Contains("Cambiar logo", panel);
        Assert.Contains("Guardar\")", panel);
        Assert.DoesNotContain("Guardar cambios", panel);
        Assert.DoesNotContain("Logo (URL)", panel);
        Assert.DoesNotContain("Texto secundario</span>", panel);
        Assert.DoesNotContain("Términos y condiciones</span>", panel);
        Assert.DoesNotContain("Mensaje al pie</span>", panel);
        Assert.DoesNotContain("<span>Moneda</span>", panel);
        Assert.Contains("secondaryText=settings.SecondaryText", panel);
        Assert.Contains("terms=settings.Terms", panel);
        Assert.Contains("footerMessage=settings.FooterMessage", panel);
        Assert.Contains("currency,displayName,primaryColor,textColor,logoUrl,secondaryText,terms,footerMessage", panel);
        Assert.Contains("PreviewLogoUrl=>ResolveLogoDisplayUrl(logoUrl)", panel);

        var logoUpload = panel.IndexOf("Logo de la tarjeta", StringComparison.Ordinal);
        var save = panel.IndexOf("@onclick=\"Save\"", StringComparison.Ordinal);
        Assert.True(logoUpload < save);
    }

    [Fact]
    public void GiftCardSettingsPanel_RendersSaveFeedbackBelowBottomSaveButton()
    {
        var panel = Read("src", "LoyaltyCloud.Admin", "Components", "GiftCardSettingsPanel.razor");

        var save = panel.IndexOf("@onclick=\"Save\"", StringComparison.Ordinal);
        var error = panel.IndexOf("@if(error is not null)", StringComparison.Ordinal);
        var success = panel.IndexOf("@if(success is not null)", StringComparison.Ordinal);

        Assert.True(save > 0);
        Assert.True(save < error);
        Assert.True(save < success);
        Assert.Equal(1, panel.Split("@if(error is not null)", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, panel.Split("@if(success is not null)", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void ConfigurationTabs_RenderSaveFeedbackBelowTheirSaveActions()
    {
        var source = Read("src", "LoyaltyCloud.Admin", "Pages", "Config.razor");

        var digitalSave = source.IndexOf("SaveWalletBrandingAsync", StringComparison.Ordinal);
        var pointsSection = source.IndexOf("Reglas de puntos y beneficios", StringComparison.Ordinal);
        var pointsSave = source.IndexOf("SaveAsync", pointsSection, StringComparison.Ordinal);
        var firstSuccess = source.IndexOf("@if (successMsg is not null)", StringComparison.Ordinal);
        var firstError = source.IndexOf("@if (errorMsg is not null)", StringComparison.Ordinal);
        var secondSuccess = source.IndexOf("@if (successMsg is not null)", firstSuccess + 1, StringComparison.Ordinal);
        var secondError = source.IndexOf("@if (errorMsg is not null)", firstError + 1, StringComparison.Ordinal);

        Assert.True(digitalSave > 0);
        Assert.True(pointsSave > pointsSection);
        Assert.True(digitalSave < firstSuccess);
        Assert.True(digitalSave < firstError);
        Assert.True(pointsSave < secondSuccess);
        Assert.True(pointsSave < secondError);
        Assert.Equal(2, source.Split("@if (successMsg is not null)", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, source.Split("@if (errorMsg is not null)", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void GiftCardPublicClaimPreview_UsesActualCardDataWithCleanWalletFront()
    {
        var claim = Read("src", "LoyaltyCloud.Admin", "Pages", "GiftCardClaim.razor");

        Assert.Contains("DisplayName=\"@claim.DisplayName\"", claim);
        Assert.Contains("BackgroundColor=\"@claim.PrimaryColor\"", claim);
        Assert.Contains("TextColor=\"@claim.TextColor\"", claim);
        Assert.Contains("LogoUrl=\"@claim.LogoUrl\"", claim);
        Assert.Contains("SenderName=\"@claim.Card.SenderName\"", claim);
        Assert.Contains("Balance=\"@Money(claim.Card.CurrentBalance, claim.Card.Currency)\"", claim);
        Assert.Contains("Expiration=\"@ExpirationText\"", claim);
        Assert.Contains("ShowCategoryLabel=\"false\"", claim);
        Assert.Contains("ShowSecondaryText=\"false\"", claim);
        Assert.Contains("ShowRecipient=\"false\"", claim);
        Assert.Contains("ShowBalanceLabel=\"false\"", claim);
        Assert.Contains("ShowCurrency=\"false\"", claim);
        Assert.DoesNotContain("SecondaryText=\"@claim.SecondaryText\"", claim);
        Assert.DoesNotContain("RecipientName=\"@claim.Card.RecipientName\"", claim);
        Assert.DoesNotContain("Balance=\"@claim.Card.CurrentBalance.ToString(\"N2\")\"", claim);
        Assert.Contains("decimal.Truncate(value)==value", claim);
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
        var pages = new[] { "GiftCards.razor", "GiftCardIssue.razor", "GiftCardRedeem.razor", "GiftCardList.razor", "GiftCardDetail.razor", "GiftCardReports.razor" };
        foreach (var page in pages)
            Assert.Contains("Authorize(Policy = LoyaltyCloud.Admin.Auth.GiftCardsAuthorization.Policy)", Read("src", "LoyaltyCloud.Admin", "Pages", page));
    }

    [Fact]
    public void GiftCardUi_PreservesCustomerDeliveryAndRemovesAdminCreditEntryPoints()
    {
        var detail = Read("src", "LoyaltyCloud.Admin", "Pages", "GiftCardDetail.razor");
        var issue = Read("src", "LoyaltyCloud.Admin", "Pages", "GiftCardIssue.razor");
        var claim = Read("src", "LoyaltyCloud.Admin", "Pages", "GiftCardClaim.razor");
        Assert.DoesNotContain("AdjustAsync", detail);
        Assert.DoesNotContain("Agregar a Apple Wallet", detail);
        Assert.DoesNotContain("Agregar a Google Wallet", detail);
        Assert.Contains("RedeemAsync", detail);
        Assert.Contains("Confirmar canje", detail);
        Assert.Contains("Cancelar tarjeta de regalo", detail);
        Assert.Contains("Reenviar por email", detail);
        Assert.Contains("RotateClaimTokenAsync", detail);
        Assert.Contains("Reenviar por email", issue);
        Assert.Contains("RotateClaimTokenAsync", issue);
        Assert.Contains("Delivery.SendEmailAsync", issue);
        Assert.Contains("IHttpContextAccessor", claim);
        Assert.Contains("Request.Headers.UserAgent", claim);
        Assert.Contains("Agregar a Wallet", claim);
        Assert.Contains("/wallet/apple", claim);
        Assert.Contains("/wallet/google", claim);
        Assert.DoesNotContain("loyaltyGiftCardWallet.getUserAgent", claim);
        Assert.DoesNotContain("@onclick=\"ShowPhoneGuidance\"", claim);
        Assert.Contains("kb-gift-message-field", issue);
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
