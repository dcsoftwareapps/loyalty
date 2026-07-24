extern alias AdminApp;

using System.Net;
using AdminApp::LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using LoyaltyCloud.Infrastructure.Services;
using LoyaltyCloud.Tests.Integration.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class AdminRoutingTests : IClassFixture<AdminRoutingTests.AdminWebApplicationFactory>, IAsyncLifetime
{
    private const string SuperAdminUsername = "platform";
    private const string SuperAdminPassword = "Platform123!";
    private const string TenantAdminUsername = "owner";
    private const string TenantAdminPassword = "Tenant123!";

    private readonly AdminWebApplicationFactory _factory;

    public AdminRoutingTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync() => await _factory.EnsureDatabaseCreatedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Root_redirects_to_platform_login_without_loop()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/platform/login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Platform_login_is_anonymous()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/platform/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Tenant_login_route_is_anonymous()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/kbeauty/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Anonymous_platform_route_redirects_to_platform_login_without_double_platform()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/platform/tenants");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("/platform/login?returnUrl=%2Fplatform%2Ftenants", location.OriginalString);
        Assert.DoesNotContain("/platform/platform/login", location.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.Equals("/login", location.OriginalString, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Anonymous_slugless_admin_route_does_not_redirect_to_legacy_login()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/scan");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("/platform/login", location.AbsolutePath);
        Assert.Equal("?returnUrl=%2Fscan", location.Query);
        Assert.DoesNotContain("/login?ReturnUrl=", location.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Tenant_cookie_redirect_preserves_tenant_slug()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("admin.test");
        context.Request.Path = "/kbeauty/dashboard";

        var redirect = AdminLoginRedirects.BuildTenantAwareLoginRedirect(
            context.Request,
            "https://admin.test/login?ReturnUrl=%2Fkbeauty%2Fdashboard");

        Assert.Equal("/kbeauty/login?returnUrl=%2Fkbeauty%2Fdashboard", redirect);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Super_admin_authenticated_can_access_platform_tenants()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", await _factory.CreateSuperAdminCookieAsync());

        using var response = await client.GetAsync("/platform/tenants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Tenant_admin_authenticated_cannot_access_platform_tenants()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", await _factory.CreateTenantAdminCookieAsync());

        using var response = await client.GetAsync("/platform/tenants");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("/platform/login?returnUrl=%2Fplatform%2Ftenants", location.OriginalString);
        Assert.DoesNotContain("/platform/platform/login", location.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Tenant_cookie_redirect_without_slug_uses_platform_login_not_legacy_login()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("admin.test");
        context.Request.Path = "/dashboard";

        var redirect = AdminLoginRedirects.BuildTenantAwareLoginRedirect(
            context.Request,
            "https://admin.test/login?ReturnUrl=%2Fdashboard");

        Assert.Equal("/platform/login", redirect);
        Assert.DoesNotContain("/login?ReturnUrl=", redirect, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    [Trait("Category", "AdminCustomerPoints")]
    public void Customer_detail_points_button_links_to_existing_scan_flow_with_serial_prefill()
    {
        var customerDetailSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "CustomerDetail.razor"));
        var scanSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Scan.razor"));

        Assert.Contains("href=\"@ScanHref()\"", customerDetailSource);
        Assert.Contains("/scan?serial=", customerDetailSource);
        Assert.Contains("Uri.EscapeDataString(detail.Wallet.SerialNumber)", customerDetailSource);
        Assert.DoesNotContain("Nav.NavigateTo($\"/scan?serial=", customerDetailSource, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery] public string? Serial", scanSource);
        Assert.Contains("await SearchAsync();", scanSource);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    [Trait("Category", "AdminCustomerPoints")]
    public void Direct_scan_route_remains_available_for_general_add_points_flow()
    {
        var scanSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Scan.razor"));

        Assert.Contains("@page \"/scan\"", scanSource);
        Assert.Contains("Escanear QR", scanSource);
        Assert.Contains("Serial de la clienta", scanSource);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public void Redeem_route_is_visible_in_navigation_and_history_remains_available()
    {
        var layoutSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Components", "Layout", "MainLayout.razor"));
        var redeemSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Redeem.razor"));
        var redemptionsSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Redemptions.razor"));

        Assert.Contains("href=\"/redeem\"", layoutSource);
        Assert.Contains(">Canjear</NavLink>", layoutSource);
        Assert.Contains("@page \"/redeem\"", redeemSource);
        Assert.Contains("@page \"/redemptions\"", redemptionsSource);
        Assert.Contains("href=\"/redemptions\"", redeemSource);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public void Redeem_uses_existing_qr_scanner_and_manual_serial_fallback()
    {
        var redeemSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Redeem.razor"));

        Assert.Contains("Escanear QR", redeemSource);
        Assert.Contains("kbeautyQrScanner.start", redeemSource);
        Assert.Contains("kbeautyQrScanner.stop", redeemSource);
        Assert.Contains("[JSInvokable]", redeemSource);
        Assert.Contains("public async Task OnQrDetected(string rawValue)", redeemSource);
        Assert.Contains("private static string? ExtractSerial", redeemSource);
        Assert.Contains("Serial de la clienta", redeemSource);
        Assert.Contains("placeholder=\"KB-A7B9C2X\"", redeemSource);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public void Redeem_uses_admin_api_for_catalog_and_redemption_instead_of_mediatr()
    {
        var redeemSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Redeem.razor"));

        Assert.Contains("@inject AdminApiClient AdminApi", redeemSource);
        Assert.Contains("AdminApi.GetAsync<CustomerDetailDto>", redeemSource);
        Assert.Contains("api/customers/{Uri.EscapeDataString(serial)}", redeemSource);
        Assert.Contains("AdminApi.GetAsync<IReadOnlyList<RewardCatalogItemDto>>", redeemSource);
        Assert.Contains("api/redemptions/catalog/{Uri.EscapeDataString(serial)}", redeemSource);
        Assert.Contains("AdminApi.PostAsJsonAsync<RedeemRewardRequest, RedemptionResponse>", redeemSource);
        Assert.Contains("\"api/redemptions\"", redeemSource);
        Assert.DoesNotContain("@inject ISender", redeemSource);
        Assert.DoesNotContain("new RedeemRewardCommand", redeemSource);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public void Redeem_blocks_ineligible_rewards_double_submit_and_refreshes_after_success()
    {
        var redeemSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Redeem.razor"));

        Assert.Contains("catalog?.Where(r => r.CanAfford).ToList()", redeemSource);
        Assert.Contains("catalog?.Where(r => !r.CanAfford).ToList()", redeemSource);
        Assert.Contains("if (!reward.CanAfford || busy)", redeemSource);
        Assert.Contains("disabled=\"@(busy || selectedReward is not null)\"", redeemSource);
        Assert.Contains("if (selectedReward is null || customer is null || busy)", redeemSource);
        Assert.Contains("await RefreshAfterRedemptionAsync(serial);", redeemSource);
        Assert.Contains("success = result.Value;", redeemSource);
        Assert.Contains("errorMessage = result.Error;", redeemSource);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public void Admin_login_redirects_treat_redeem_as_reserved_tenant_route()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Auth", "AdminLoginRedirects.cs"));

        Assert.Contains("value.Equals(\"redeem\", StringComparison.OrdinalIgnoreCase)", source);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public void Api_redemptions_use_admin_hmac_and_existing_wallet_refresh()
    {
        var middlewareSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.API", "Middleware", "AdminApiAuthenticationMiddleware.cs"));
        var handlerSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Application", "Redemptions", "Commands", "RedeemReward", "RedeemRewardHandler.cs"));
        var controllerSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.API", "Controllers", "RedemptionsController.cs"));

        Assert.Contains("request.Path.StartsWithSegments(\"/api/redemptions\", StringComparison.OrdinalIgnoreCase)", middlewareSource);
        Assert.Contains("card.RedeemPoints(reward.PointsCost);", handlerSource);
        Assert.Contains("card.Touch(_dt);", handlerSource);
        Assert.Contains("await TryPushWalletUpdateAsync(card.SerialNumber, ct);", handlerSource);
        Assert.Contains("PassUpdateReason.RedemptionConfirmed", handlerSource);
        Assert.Contains("[HttpPut(\"{id:guid}/cancel\")]", controllerSource);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    [Trait("Category", "AdminCustomerPoints")]
    public void Scan_amount_input_updates_component_state_and_button_text()
    {
        var scanSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Scan.razor"));

        Assert.Contains("value=\"@amountInput\"", scanSource);
        Assert.Contains("@oninput=\"HandleAmountInput\"", scanSource);
        Assert.Contains("private void HandleAmountInput(ChangeEventArgs e)", scanSource);
        Assert.Contains("private decimal PurchaseAmount", scanSource);
        Assert.Contains("private bool IsPurchaseAmountValid", scanSource);
        Assert.Contains("Confirmar compra de ${PurchaseAmount:0.00}", scanSource);
        Assert.Contains("disabled=\"@(!IsPurchaseAmountValid || busy)\"", scanSource);
        Assert.DoesNotContain("@bind=\"amount\"", scanSource, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    [Trait("Category", "AdminCustomerPoints")]
    public void Scan_confirm_guard_prevents_invalid_or_double_submit()
    {
        var scanSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Scan.razor"));

        Assert.Contains("if (customer is null || !IsPurchaseAmountValid || busy) return;", scanSource);
        Assert.Contains("await PointsApi.AddPointsAsync(serial, PurchaseAmount)", scanSource);
        Assert.Contains("var refreshed = await FetchCustomerAsync(serial);", scanSource);
        Assert.Contains("customer = refreshed.Value;", scanSource);
        Assert.DoesNotContain("new AddPointsCommand(customer.SerialNumber, PurchaseAmount, \"admin-panel\")", scanSource);
    }

    [Fact]
    [Trait("Category", "AdminInteractiveTenantContext")]
    [Trait("Category", "AdminNotificationsCleanup")]
    public void Marketing_notifications_uses_signed_admin_api_client()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "MarketingNotifications.razor"));

        Assert.Contains("@inject AdminApiClient Api", source);
        Assert.Contains("Api.GetAsync<List<CustomNotificationCampaignDto>>", source);
        Assert.Contains("Api.PostAsJsonAsync<PreviewCustomNotificationAudienceRequest, CustomNotificationAudiencePreviewDto>", source);
        Assert.Contains("Api.PostAsJsonAsync<CustomNotificationCampaignRequest, CustomNotificationCampaignDto>", source);
        Assert.DoesNotContain("@inject IHttpClientFactory", source);
        Assert.DoesNotContain("HttpClientFactory.CreateClient", source);
    }

    [Fact]
    [Trait("Category", "AdminMarketingNotifications")]
    public void Marketing_notifications_form_only_requires_visible_message()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "MarketingNotifications.razor"));

        Assert.Contains("<label for=\"message\">Mensaje</label>", source);
        Assert.Contains("@bind=\"form.Message\"", source);
        Assert.Contains("@bind:event=\"oninput\"", source);
        Assert.DoesNotContain("id=\"campaign-name\"", source);
        Assert.DoesNotContain("id=\"campaign-title\"", source);
        Assert.DoesNotContain("id=\"short-message\"", source);
        Assert.DoesNotContain("id=\"long-message\"", source);
        Assert.DoesNotContain("Nombre interno", source);
        Assert.DoesNotContain("Mensaje corto", source);
        Assert.DoesNotContain("Mensaje largo", source);
        Assert.DoesNotContain("public string Name { get; set; }", source);
        Assert.DoesNotContain("public string Title { get; set; }", source);
        Assert.DoesNotContain("public string ShortMessage { get; set; }", source);
        Assert.DoesNotContain("public string LongMessage { get; set; }", source);
        Assert.Contains("public string Message { get; set; }", source);
    }

    [Fact]
    [Trait("Category", "AdminMarketingNotifications")]
    public void Marketing_notifications_autogenerates_backend_name_and_title()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "MarketingNotifications.razor"));

        Assert.Contains("private const string GeneratedTitle = \"NOVEDAD\";", source);
        Assert.Contains("var generatedName = await GenerateInternalNameAsync();", source);
        Assert.Contains("generatedName,", source);
        Assert.Contains("GeneratedTitle,", source);
        Assert.Contains("BuildShortMessage(message),", source);
        Assert.Contains("message,", source);
        Assert.Contains("new CustomNotificationCampaignRequest(", source);
        Assert.DoesNotContain("form.Name.Trim()", source);
        Assert.DoesNotContain("form.Title.Trim()", source);
        Assert.DoesNotContain("form.ShortMessage.Trim()", source);
        Assert.DoesNotContain("form.LongMessage.Trim()", source);
    }

    [Fact]
    [Trait("Category", "AdminMarketingNotifications")]
    public void Marketing_notifications_uses_tenant_timezone_for_generated_name()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "MarketingNotifications.razor"));

        Assert.Contains("@inject AuthenticationStateProvider AuthenticationStateProvider", source);
        Assert.Contains("@inject ITenantRepository Tenants", source);
        Assert.Contains("AdminClaimTypes.TenantId", source);
        Assert.Contains("Tenants.GetByIdAsync(tenantId)", source);
        Assert.Contains("tenant.TimeZoneId", source);
        Assert.Contains("TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tenantTimeZone)", source);
        Assert.Contains("dd/MM/yyyy HH:mm", source);
        Assert.Contains("CultureInfo.InvariantCulture", source);
        Assert.DoesNotContain("DateTime.Now", source);
    }

    [Fact]
    [Trait("Category", "AdminMarketingNotifications")]
    public void Marketing_notifications_preview_uses_generated_title_and_message_without_unavailable_filler()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "MarketingNotifications.razor"));

        Assert.Contains("<h3 style=\"margin-top:8px;\">@GeneratedTitle</h3>", source);
        Assert.Contains("@BuildShortMessage(form.Message)", source);
        Assert.Contains("@form.Message.Trim()", source);
        Assert.DoesNotContain("@DisplayText(form.ShortMessage)", source);
        Assert.DoesNotContain("@DisplayText(form.LongMessage)", source);
    }

    [Fact]
    [Trait("Category", "AdminNotificationsCleanup")]
    public void Notifications_is_hidden_from_tenant_admin_navigation_but_messages_remains_visible()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Components", "Layout", "MainLayout.razor"));

        Assert.DoesNotContain("href=\"/notifications\"", source);
        Assert.DoesNotContain(">Notificaciones</NavLink>", source);
        Assert.Contains("href=\"/marketing-notifications\"", source);
        Assert.Contains(">Mensajes</NavLink>", source);
    }

    [Fact]
    [Trait("Category", "AdminNotificationsCleanup")]
    public void Notifications_route_remains_available_for_internal_history()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Notifications.razor"));

        Assert.Contains("@page \"/notifications\"", source);
        Assert.Contains("new ListNotificationsQuery(Status: status, Type: type, Take: 100)", source);
        Assert.Contains("new GetNotificationMetricsQuery()", source);
        Assert.Contains("metrics.Pending", source);
        Assert.Contains("metrics.Processed", source);
        Assert.Contains("metrics.Failed", source);
        Assert.Contains("metrics.CustomersReached", source);
        Assert.Contains("metrics.PushesAttempted", source);
        Assert.Contains("metrics.PushesFailed", source);
    }

    [Fact]
    [Trait("Category", "AdminNotificationsCleanup")]
    public void Notifications_legacy_manual_form_is_not_visible()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Notifications.razor"));

        Assert.DoesNotContain("Nueva notificación manual", source);
        Assert.DoesNotContain("En Fase 5.1 Apple Wallet solo refresca el pass", source);
        Assert.DoesNotContain("notification-serial", source);
        Assert.DoesNotContain("notification-title", source);
        Assert.DoesNotContain("notification-until", source);
        Assert.DoesNotContain("notification-message", source);
        Assert.DoesNotContain("Crear y procesar", source);
        Assert.DoesNotContain("@onclick=\"ToggleForm\"", source);
    }

    [Fact]
    [Trait("Category", "AdminNotificationsCleanup")]
    public void Point_campaign_started_notifications_are_due_only_for_active_campaigns_and_deduplicated()
    {
        var readService = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Infrastructure", "Services", "PointCampaignNotificationReadService.cs"));
        var handler = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Application", "Notifications", "Commands", "CreatePointCampaignStartedNotifications", "CreatePointCampaignStartedNotificationsHandler.cs"));
        var scheduler = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.API", "Services", "LoyaltyMaintenanceBackgroundService.cs"));

        Assert.Contains("c.StartsAtUtc <= nowUtc", readService);
        Assert.Contains("c.EndsAtUtc >= nowUtc", readService);
        Assert.Contains("BuildCorrelationId(x.Campaign!.Id, x.Card.SerialNumber)", readService);
        Assert.Contains("n.Type == NotificationType.PointCampaignStarted", readService);
        Assert.Contains("existing.Contains(correlationId)", readService);
        Assert.Contains("if (candidate.AlreadyNotified)", handler);
        Assert.Contains("NotificationType.PointCampaignStarted", handler);
        Assert.Contains("CorrelationId: candidate.CorrelationId", handler);
        Assert.Contains("ProcessImmediately: true", handler);
        Assert.Contains("new CreatePointCampaignStartedNotificationsCommand(OperatorId, timeZoneId)", scheduler);
    }

    [Fact]
    [Trait("Category", "AdminNotificationsCleanup")]
    public void Monthly_product_started_notifications_are_due_only_for_current_product_and_deduplicated()
    {
        var readService = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Infrastructure", "Services", "MonthlyProductNotificationReadService.cs"));
        var handler = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Application", "Notifications", "Commands", "CreateMonthlyProductStartedNotifications", "CreateMonthlyProductStartedNotificationsHandler.cs"));
        var scheduler = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.API", "Services", "LoyaltyMaintenanceBackgroundService.cs"));

        Assert.Contains("r.IsMonthlyProduct", readService);
        Assert.Contains("r.ValidFrom.Value <= nowUtc", readService);
        Assert.Contains("r.ValidTo.Value >= nowUtc", readService);
        Assert.Contains("BuildCorrelationId(product.Id, x.SerialNumber)", readService);
        Assert.Contains("n.Type == NotificationType.MonthlyProductStarted", readService);
        Assert.Contains("existing.Contains(correlationId)", readService);
        Assert.Contains("if (candidate.AlreadyNotified)", handler);
        Assert.Contains("NotificationType.MonthlyProductStarted", handler);
        Assert.Contains("CorrelationId: candidate.CorrelationId", handler);
        Assert.Contains("ProcessImmediately: true", handler);
        Assert.Contains("new CreateMonthlyProductStartedNotificationsCommand(OperatorId, timeZoneId)", scheduler);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Admin_cookie_options_do_not_use_legacy_root_login_path()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Program.cs"));

        Assert.DoesNotContain("LoginPath = \"/login\"", source);
        Assert.DoesNotContain("AccessDeniedPath = \"/login\"", source);
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src")))
            current = current.Parent;

        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    public sealed class AdminWebApplicationFactory : WebApplicationFactory<AdminApp::Program>
    {
        private readonly string _dbName = "LoyaltyCloudAdminRouting-" + Guid.NewGuid().ToString("N");
        private readonly FakeApnService _apn = new();
        private readonly FakeStorageService _storage = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=(test);Database=Test;",
                    ["Admin:ApiBaseUrl"] = "https://api.test/",
                    ["AdminApi:SharedSecret"] = "test-admin-api-shared-secret-with-enough-length",
                    ["Azure:KeyVaultUri"] = "",
                    ["Azure:BlobStorage:ConnectionString"] = "",
                    ["Apple:PassTypeIdentifier"] = "pass.com.kbeautymx.loyalty",
                    ["Apple:TeamIdentifier"] = "TESTTEAM01",
                    ["Apple:WebServiceURL"] = "https://api.test",
                    ["Apple:OrganizationName"] = "LoyaltyCloud Test",
                    ["Wallet:UseRealPassSigning"] = "false",
                    ["Wallet:UseRealApns"] = "false",
                    ["SuperAdmin:Username"] = SuperAdminUsername,
                    ["SuperAdmin:PasswordHash"] = new PasswordHashingService().HashPassword(SuperAdminPassword),
                    ["SuperAdmin:SessionHours"] = "8"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(_dbName));

                services.RemoveAll<IPassGeneratorService>();
                services.RemoveAll<IApnService>();
                services.RemoveAll<IStorageService>();

                services.AddSingleton<IPassGeneratorService, FakePassGeneratorService>();
                services.AddSingleton<IApnService>(_apn);
                services.AddSingleton<IStorageService>(_storage);
            });
        }

        public async Task EnsureDatabaseCreatedAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == TenantSeed.KBeautyTenantId);
            db.Entry(subscription).Property(nameof(TenantSubscription.PaidThroughUtc)).CurrentValue = DateTime.UtcNow.AddDays(30);

            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
            if (!await db.TenantAdminUsers.AnyAsync(u => u.TenantId == TenantSeed.KBeautyTenantId && u.NormalizedUsername == TenantAdminUser.NormalizeUsername(TenantAdminUsername)))
            {
                var passwords = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
                db.TenantAdminUsers.Add(new TenantAdminUser(
                    Guid.Parse("b4000000-0000-0000-0000-000000009001"),
                    TenantSeed.KBeautyTenantId,
                    TenantAdminUsername,
                    passwords.HashPassword(TenantAdminPassword),
                    DateTime.UtcNow));
            }

            await db.SaveChangesAsync();
        }

        public async Task<string> CreateSuperAdminCookieAsync()
        {
            using var scope = Services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);
            var result = await scope.ServiceProvider.GetRequiredService<SuperAdminAuthService>()
                .TrySignInAsync(context, SuperAdminUsername, SuperAdminPassword);

            Assert.Equal(SuperAdminLoginResult.Success, result);
            return ExtractCookie(context, "loyaltycloud.platform.auth");
        }

        public async Task<string> CreateTenantAdminCookieAsync()
        {
            using var scope = Services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);
            var result = await scope.ServiceProvider.GetRequiredService<AdminAuthService>()
                .TrySignInAsync(context, TenantSeed.KBeautySlug, TenantAdminUsername, TenantAdminPassword);

            Assert.Equal(AdminLoginResult.Success, result);
            return ExtractCookie(context, "loyaltycloud.admin.auth");
        }

        private static DefaultHttpContext CreateHttpContext(IServiceProvider services)
        {
            var context = new DefaultHttpContext
            {
                RequestServices = services
            };
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("admin.test");
            return context;
        }

        private static string ExtractCookie(DefaultHttpContext context, string cookieName)
        {
            var setCookie = context.Response.Headers.SetCookie
                .FirstOrDefault(value => value?.StartsWith(cookieName + "=", StringComparison.OrdinalIgnoreCase) == true);

            Assert.False(string.IsNullOrWhiteSpace(setCookie));
            return setCookie!.Split(';', 2)[0];
        }
    }
}
