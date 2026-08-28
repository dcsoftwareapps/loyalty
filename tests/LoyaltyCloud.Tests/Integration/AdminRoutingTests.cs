extern alias AdminApp;

using System.Net;
using System.Text.RegularExpressions;
using AdminApp::LoyaltyCloud.Admin.Auth;
using AdminApp::LoyaltyCloud.Admin.Middleware;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
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

    [Theory]
    [Trait("Category", "AdminRouting")]
    [InlineData("/tenant/billing")]
    [InlineData("/tenant/billing/payment/success")]
    [InlineData("/tenant/billing/payment/cancelled")]
    [InlineData("/logout")]
    public void Suspended_tenant_allowed_paths_are_limited_to_billing(string path)
    {
        Assert.True(AdminTenantContextMiddleware.IsBillingPath(path));
    }

    [Theory]
    [Trait("Category", "AdminRouting")]
    [InlineData("/tenant/dashboard")]
    [InlineData("/tenant/custom/billing/report")]
    [InlineData("/tenant/billing/settings")]
    public void Suspended_tenant_operational_or_similar_paths_are_blocked(string path)
    {
        Assert.False(AdminTenantContextMiddleware.IsBillingPath(path));
    }
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
    public async Task Development_developer_login_is_available_and_signs_in_as_super_admin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var getLogin = await client.GetAsync("/platform/login");
        var loginHtml = await getLogin.Content.ReadAsStringAsync();
        Assert.Contains("Acceso de desarrollo", loginHtml);
        Assert.Contains("action=\"/platform/developer-login\"", loginHtml);

        var form = ExtractHiddenInputs(loginHtml);
        using var post = new HttpRequestMessage(HttpMethod.Post, "/platform/developer-login")
        {
            Content = new FormUrlEncodedContent(form)
        };
        post.Headers.Add("Cookie", ExtractCookies(getLogin));

        using var loginResponse = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/platform/tenants", loginResponse.Headers.Location?.OriginalString);
        var platformCookie = ExtractCookie(loginResponse, "loyaltycloud.platform.auth");

        using var protectedRequest = new HttpRequestMessage(HttpMethod.Get, "/platform/tenants");
        protectedRequest.Headers.Add("Cookie", platformCookie);
        using var protectedResponse = await client.SendAsync(protectedRequest);
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
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
    [Trait("Category", "TenantAdminAuth")]
    public async Task Platform_login_cookie_round_trip_allows_platform_tenants()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var getLogin = await client.GetAsync("/platform/login");
        var loginHtml = await getLogin.Content.ReadAsStringAsync();
        var antiForgeryCookies = ExtractCookies(getLogin);

        using var post = new HttpRequestMessage(HttpMethod.Post, "/platform/login")
        {
            Content = new FormUrlEncodedContent(BuildLoginForm(loginHtml, SuperAdminUsername, SuperAdminPassword))
        };
        post.Headers.Add("Cookie", antiForgeryCookies);

        using var loginResponse = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/platform/tenants", loginResponse.Headers.Location?.OriginalString);
        var platformCookie = ExtractCookie(loginResponse, "loyaltycloud.platform.auth");

        using var protectedRequest = new HttpRequestMessage(HttpMethod.Get, "/platform/tenants");
        protectedRequest.Headers.Add("Cookie", platformCookie);

        using var protectedResponse = await client.SendAsync(protectedRequest);

        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
        Assert.Null(protectedResponse.Headers.Location);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    [Trait("Category", "TenantAdminAuth")]
    public async Task Tenant_admin_login_cookie_round_trip_allows_dashboard()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var getLogin = await client.GetAsync("/kbeauty/login");
        var loginHtml = await getLogin.Content.ReadAsStringAsync();
        var antiForgeryCookies = ExtractCookies(getLogin);

        using var post = new HttpRequestMessage(HttpMethod.Post, "/kbeauty/login")
        {
            Content = new FormUrlEncodedContent(BuildLoginForm(loginHtml, TenantAdminUsername, TenantAdminPassword))
        };
        post.Headers.Add("Cookie", antiForgeryCookies);

        using var loginResponse = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/dashboard", loginResponse.Headers.Location?.OriginalString);
        var adminCookie = ExtractCookie(loginResponse, "loyaltycloud.admin.auth");

        using var protectedRequest = new HttpRequestMessage(HttpMethod.Get, "/dashboard");
        protectedRequest.Headers.Add("Cookie", adminCookie);

        using var protectedResponse = await client.SendAsync(protectedRequest);

        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
        Assert.Null(protectedResponse.Headers.Location);
    }

    [Theory]
    [InlineData(TenantSuspensionReason.PaymentPastDue)]
    [InlineData(TenantSuspensionReason.TrialExpired)]
    [Trait("Category", "AdminRouting")]
    [Trait("Category", "TenantAdminAuth")]
    public async Task Billing_suspended_tenant_can_login_and_is_limited_to_billing(TenantSuspensionReason reason)
    {
        await _factory.SetKBeautySubscriptionAsync(
            TenantSubscriptionStatus.Suspended,
            reason);

        try
        {
            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            using var getLogin = await client.GetAsync("/kbeauty/login");
            var loginHtml = await getLogin.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, getLogin.StatusCode);
            Assert.DoesNotContain("Este programa está suspendido temporalmente.", loginHtml);

            using var post = new HttpRequestMessage(HttpMethod.Post, "/kbeauty/login")
            {
                Content = new FormUrlEncodedContent(BuildLoginForm(
                    loginHtml,
                    TenantAdminUsername,
                    TenantAdminPassword))
            };
            post.Headers.Add("Cookie", ExtractCookies(getLogin));

            using var loginResponse = await client.SendAsync(post);
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
            Assert.Equal("/kbeauty/billing", loginResponse.Headers.Location?.OriginalString);
            var cookie = ExtractCookie(loginResponse, "loyaltycloud.admin.auth");

            using var billingRequest = new HttpRequestMessage(HttpMethod.Get, "/kbeauty/billing");
            billingRequest.Headers.Add("Cookie", cookie);
            using var billingResponse = await client.SendAsync(billingRequest);
            Assert.Equal(HttpStatusCode.OK, billingResponse.StatusCode);
            var billingHtml = await billingResponse.Content.ReadAsStringAsync();
            Assert.Contains("<!--Blazor:", billingHtml);

            using var frameworkRequest = new HttpRequestMessage(HttpMethod.Get, "/_framework/blazor.web.js");
            frameworkRequest.Headers.Add("Cookie", cookie);
            using var frameworkResponse = await client.SendAsync(frameworkRequest);
            Assert.Equal(HttpStatusCode.OK, frameworkResponse.StatusCode);
            Assert.Null(frameworkResponse.Headers.Location);
            Assert.Equal("text/javascript", frameworkResponse.Content.Headers.ContentType?.MediaType);

            using var blazorRequest = new HttpRequestMessage(HttpMethod.Get, "/_blazor");
            blazorRequest.Headers.Add("Cookie", cookie);
            using var blazorResponse = await client.SendAsync(blazorRequest);
            Assert.NotEqual(HttpStatusCode.Redirect, blazorResponse.StatusCode);
            Assert.Null(blazorResponse.Headers.Location);

            using var dashboardRequest = new HttpRequestMessage(HttpMethod.Get, "/dashboard");
            dashboardRequest.Headers.Add("Cookie", cookie);
            using var dashboardResponse = await client.SendAsync(dashboardRequest);
            Assert.Equal(HttpStatusCode.Redirect, dashboardResponse.StatusCode);
            Assert.Equal("/kbeauty/billing", dashboardResponse.Headers.Location?.OriginalString);

            using var customersRequest = new HttpRequestMessage(HttpMethod.Get, "/customers");
            customersRequest.Headers.Add("Cookie", cookie);
            using var customersResponse = await client.SendAsync(customersRequest);
            Assert.Equal(HttpStatusCode.Redirect, customersResponse.StatusCode);
            Assert.Equal("/kbeauty/billing", customersResponse.Headers.Location?.OriginalString);
        }
        finally
        {
            await _factory.SetKBeautySubscriptionAsync(TenantSubscriptionStatus.Active, null);
        }
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    [Trait("Category", "TenantAdminAuth")]
    public async Task Payment_suspended_tenant_still_rejects_invalid_credentials()
    {
        await _factory.SetKBeautySubscriptionAsync(
            TenantSubscriptionStatus.Suspended,
            TenantSuspensionReason.PaymentPastDue);

        try
        {
            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            using var getLogin = await client.GetAsync("/kbeauty/login");
            var loginHtml = await getLogin.Content.ReadAsStringAsync();
            using var post = new HttpRequestMessage(HttpMethod.Post, "/kbeauty/login")
            {
                Content = new FormUrlEncodedContent(BuildLoginForm(
                    loginHtml,
                    TenantAdminUsername,
                    "wrong-password"))
            };
            post.Headers.Add("Cookie", ExtractCookies(getLogin));

            using var response = await client.SendAsync(post);
            var responseHtml = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("incorrectos.", responseHtml);
            Assert.DoesNotContain(response.Headers, header =>
                header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) &&
                header.Value.Any(value => value.StartsWith("loyaltycloud.admin.auth=", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            await _factory.SetKBeautySubscriptionAsync(TenantSubscriptionStatus.Active, null);
        }
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Billing_only_tenant_allows_Blazor_transport_and_framework_assets()
    {
        Assert.True(AdminTenantContextMiddleware.IsBillingInfrastructurePath("/_blazor"));
        Assert.True(AdminTenantContextMiddleware.IsBillingInfrastructurePath("/_blazor/negotiate"));
        Assert.True(AdminTenantContextMiddleware.IsBillingInfrastructurePath("/_framework"));
        Assert.True(AdminTenantContextMiddleware.IsBillingInfrastructurePath("/_framework/blazor.web.js"));
        Assert.False(AdminTenantContextMiddleware.IsBillingInfrastructurePath("/api/points"));
        Assert.False(AdminTenantContextMiddleware.IsBillingInfrastructurePath("/customers"));
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
        Assert.Contains("ID del cliente", scanSource);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public void Redeem_route_is_visible_in_navigation_and_history_remains_available()
    {
        var layoutSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Components", "Layout", "MainLayout.razor"));
        var redeemSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Redeem.razor"));
        var redemptionsSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Redemptions.razor"));

        Assert.Contains("href=\"/redeem\"", layoutSource);
        Assert.Contains(">Canjear puntos</span>", layoutSource);
        Assert.Contains("@page \"/redeem\"", redeemSource);
        Assert.Contains("@page \"/redemptions\"", redemptionsSource);
        Assert.Contains("href=\"/redemptions\"", redeemSource);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Tenant_admin_navigation_is_grouped_without_changing_routes()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Components", "Layout", "MainLayout.razor"));

        var orderedItems = new[]
        {
            "<span class=\"kb-sidebar-section\">Principal</span>",
            "href=\"/dashboard\"",
            "href=\"/scan\"",
            "href=\"/redeem\"",
            "<span class=\"kb-sidebar-section\">Reportes</span>",
            "href=\"/customers\"",
            "href=\"/reports/inactive-customers\"",
            "href=\"/reports/top-rewards\"",
            "<span class=\"kb-sidebar-section\">Programa</span>",
            "href=\"/redemptions\"",
            "href=\"/rewards\"",
            "href=\"/campaigns\"",
            "href=\"/marketing-notifications\"",
            "<span class=\"kb-sidebar-section\">Gestión</span>",
            "href=\"/levels\"",
            "href=\"/config\"",
            "href=\"/quick-help\""
        };

        var previousIndex = -1;
        foreach (var item in orderedItems)
        {
            var index = source.IndexOf(item, previousIndex + 1, StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"Expected menu item after previous item: {item}");
            previousIndex = index;
        }

        Assert.DoesNotContain("href=\"/operacion\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/puntos\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/clientes\"", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Quick_help_route_is_available_for_authenticated_tenant_admin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", await _factory.CreateTenantAdminCookieAsync());

        using var response = await client.GetAsync("/quick-help");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ayuda rápida", html);
        Assert.Contains("Las tareas más comunes para atender clientes.", html);
        Assert.Contains("https://admin.loyaltycloud.net/kbeauty/join", html);
        Assert.Contains("QR de registro", html);
        Assert.Contains("Escanea para registrarte", html);
        Assert.Contains("Imprimir QR", html);
        Assert.Contains("Regístrate y empieza a acumular puntos", html);
        Assert.Contains("Powered by LoyaltyCloud", html);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Quick_help_documents_real_admin_flows_without_hardcoded_tenant_branding()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "QuickHelp.razor"));

        Assert.Contains("@page \"/quick-help\"", source);
        Assert.Contains("href=\"/scan\"", source);
        Assert.Contains("href=\"/redeem\"", source);
        Assert.Contains("ID del cliente", source);
        Assert.Contains("nombre o ID del cliente", source);
        Assert.Contains("Configuration[\"Admin:PublicBaseUrl\"]", source);
        Assert.Contains("GetPublicAdminBaseUri()", source);
        Assert.Contains("{tenantSlug.Trim().ToLowerInvariant()}/join", source);
        Assert.Contains("QrCodeSvgGenerator.GenerateDataUri(registrationUrl", source);
        Assert.Contains("PrintQrAsync", source);
        Assert.Contains("window.print", source);
        Assert.Contains("BusinessName", source);
        Assert.Contains("branding?.LogoUrl", source);
        Assert.Contains("kb-poster-logo", source);
        Assert.DoesNotContain("nombre, email o ID del cliente", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Serial", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kbeauty", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bitcafe", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("loyaltycloud-admin-894839", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Customer_ui_does_not_render_customer_email_without_renaming_customer_contracts()
    {
        var root = GetRepositoryRoot();
        var customersSource = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "Customers.razor"));
        var detailSource = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "CustomerDetail.razor"));
        var cardSource = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Components", "CustomerCard.razor"));

        Assert.DoesNotContain("CustomerEmailDisplay", customersSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerEmailDisplay", detailSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerEmailDisplay", cardSource, StringComparison.Ordinal);
        Assert.DoesNotContain("<th>Email</th>", customersSource, StringComparison.Ordinal);
        Assert.DoesNotContain("@c.Email", customersSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Label=\"Email\"", detailSource, StringComparison.Ordinal);
        Assert.DoesNotContain("@detail.Summary.Email", detailSource, StringComparison.Ordinal);
        Assert.DoesNotContain("@Customer.Email", cardSource, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Platform_tenant_create_form_exposes_synchronized_color_picker_and_hex_fields()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "PlatformTenants.razor"));

        Assert.Contains("type=\"color\"", source);
        Assert.Contains("Selector de color primario", source);
        Assert.Contains("Selector de color secundario", source);
        Assert.Contains("@bind=\"form.PrimaryColor\"", source);
        Assert.Contains("@bind=\"form.SecondaryColor\"", source);
        Assert.Contains("@bind:event=\"oninput\"", source);
        Assert.Contains("SetPrimaryColor", source);
        Assert.Contains("SetSecondaryColor", source);
        Assert.Contains("NormalizePrimaryColor", source);
        Assert.Contains("NormalizeSecondaryColor", source);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Quick_help_qr_generator_encodes_registration_url_as_local_svg()
    {
        var dataUri = AdminApp::LoyaltyCloud.Admin.Services.QrCodeSvgGenerator.GenerateDataUri(
            "https://admin.loyaltycloud.net/bitcafe/join");

        Assert.StartsWith("data:image/svg+xml;utf8,", dataUri, StringComparison.Ordinal);
        Assert.Contains("svg", Uri.UnescapeDataString(dataUri), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("QR de registro", Uri.UnescapeDataString(dataUri));
        Assert.DoesNotContain("loyaltycloud-admin-894839", dataUri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public void Redeem_uses_existing_qr_scanner_and_manual_serial_fallback()
    {
        var redeemSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Redeem.razor"));
        var scannerSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "wwwroot", "js", "qr-scanner.js"));

        Assert.Contains("Escanear QR", redeemSource);
        Assert.Contains("kbeautyQrScanner.start", redeemSource);
        Assert.Contains("kbeautyQrScanner.stop", redeemSource);
        Assert.Contains("private bool scannerStarted;", redeemSource);
        Assert.Contains("if (!scannerStarted)", redeemSource);
        Assert.Contains("[JSInvokable]", redeemSource);
        Assert.Contains("public async Task OnQrDetected(string rawValue)", redeemSource);
        Assert.Contains("await StopScannerAsync();", redeemSource);
        Assert.Contains("await LoadCatalogAsync();", redeemSource);
        Assert.Contains("private static string? ExtractSerial", redeemSource);
        Assert.Contains("ID del cliente", redeemSource);
        Assert.Contains("placeholder=\"KB-A7B9C2X\"", redeemSource);
        Assert.Contains("@bind=\"serialInput\"", redeemSource);
        Assert.Contains("@bind:event=\"oninput\"", redeemSource);
        Assert.Contains("disabled=\"@(string.IsNullOrWhiteSpace(serialInput) || busy)\"", redeemSource);
        Assert.Contains("const callback = dotNetRef;", scannerSource);
        var callbackIndex = scannerSource.IndexOf("const callback = dotNetRef;", StringComparison.Ordinal);
        var stopAfterCallbackIndex = scannerSource.IndexOf("stop();", callbackIndex, StringComparison.Ordinal);
        var invokeIndex = scannerSource.IndexOf("callback?.invokeMethodAsync(\"OnQrDetected\", value);", StringComparison.Ordinal);
        Assert.True(callbackIndex >= 0);
        Assert.True(stopAfterCallbackIndex > callbackIndex);
        Assert.True(invokeIndex > stopAfterCallbackIndex);
    }

    [Fact]
    [Trait("Category", "AdminRedemptionFlow")]
    public void Redeem_uses_admin_api_for_catalog_and_redemption_instead_of_mediatr()
    {
        var redeemSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Redeem.razor"));

        Assert.Contains("@inject AdminApiClient AdminApi", redeemSource);
        Assert.Contains("@inject ILogger<Redeem> Logger", redeemSource);
        Assert.Contains("AdminApi.GetAsync<CustomerDetailDto>", redeemSource);
        Assert.Contains("api/customers/{Uri.EscapeDataString(serial)}", redeemSource);
        Assert.Contains("AdminApi.GetAsync<IReadOnlyList<RewardCatalogItemDto>>", redeemSource);
        Assert.Contains("api/redemptions/catalog/{Uri.EscapeDataString(serial)}", redeemSource);
        Assert.Contains("Redeem customer lookup failed.", redeemSource);
        Assert.Contains("Redeem customer lookup API returned failure.", redeemSource);
        Assert.Contains("AdminApi.PostAsJsonAsync<RedeemRedemptionRequest, RedemptionResponse>", redeemSource);
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
        Assert.Contains("if (qrDetected)", redeemSource);
        Assert.Contains("qrDetected = true;", redeemSource);
        Assert.Contains("qrDetected = false;", redeemSource);
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
        Assert.Contains(">Mensajes</span>", source);
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
    [Trait("Category", "AdminConfigurationCleanup")]
    public void Config_page_hides_legacy_reward_settings_from_visible_form()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Config.razor"));

        Assert.Contains("Where(IsVisibleConfigEntry)", source);
        Assert.Contains("!entry.Key.StartsWith(\"reward_\", StringComparison.OrdinalIgnoreCase)", source);
        Assert.Contains("LoyaltyConstants.ConfigKeys.ReferralBonusPoints", source);
        Assert.DoesNotContain("Costo de canje:", source);
        Assert.DoesNotContain("<code", source);
        Assert.DoesNotContain("Puntos por referido", source);
        Assert.Contains("Reglas de puntos y beneficios", source);
    }

    [Fact]
    [Trait("Category", "AdminConfigurationCleanup")]
    public void Config_page_keeps_general_program_settings_visible()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Config.razor"));

        Assert.Contains("LoyaltyConstants.ConfigKeys.PointsPerPesoUnit => \"Pesos por 1 punto\"", source);
        Assert.Contains("LoyaltyConstants.ConfigKeys.WelcomeBonusPoints => \"Puntos de bienvenida\"", source);
        Assert.Contains("LoyaltyConstants.ConfigKeys.BirthdayMultiplier => \"Multiplicador cumpleaños\"", source);
        Assert.Contains("LoyaltyConstants.ConfigKeys.PointsExpirationEnabled => \"Expiracion de puntos activa\"", source);
        Assert.Contains("LoyaltyConstants.ConfigKeys.PointsExpireAfterMonths => \"Meses de vigencia de puntos\"", source);
    }

    [Fact]
    [Trait("Category", "AdminConfigurationCleanup")]
    public void Config_page_uses_boolean_select_and_hides_audit_column()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Config.razor"));

        Assert.Contains("IsPointsExpirationEnabled(e.Key)", source);
        Assert.Contains("<select @bind=\"edited[e.Key]\"", source);
        Assert.Contains("<option value=\"true\">Sí</option>", source);
        Assert.Contains("<option value=\"false\">No</option>", source);
        Assert.Contains("new ConfigEntry(e.Key, edited[e.Key])", source);
        Assert.DoesNotContain("<th>Última actualización</th>", source);
        Assert.DoesNotContain("UpdatedAt.ToLocalTime()", source);
        Assert.DoesNotContain("por @e.UpdatedBy", source);
        Assert.DoesNotContain("admin-panel</small>", source);
        Assert.DoesNotContain("system</small>", source);
    }

    [Fact]
    [Trait("Category", "AdminConfigurationCleanup")]
    public void Reward_catalog_remains_authority_for_redemption_costs_and_monthly_product()
    {
        var redeemHandler = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Application", "Redemptions", "Commands", "RedeemReward", "RedeemRewardHandler.cs"));
        var catalogHandler = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Application", "Redemptions", "Queries", "GetRedemptionCatalog", "GetRedemptionCatalogHandler.cs"));
        var monthlyReadService = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Infrastructure", "Services", "MonthlyProductNotificationReadService.cs"));
        var rewardsPage = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Rewards.razor"));

        Assert.Contains("card.RedeemPoints(reward.PointsCost);", redeemHandler);
        Assert.Contains("pointsSpent: reward.PointsCost", redeemHandler);
        Assert.Contains("PointsCost: i.PointsCost", catalogHandler);
        Assert.Contains("_db.RewardCatalogItems", monthlyReadService);
        Assert.Contains("r.IsMonthlyProduct", monthlyReadService);
        Assert.Contains("CurrentMonthlyProduct.PointsCost", rewardsPage);
    }

    [Fact]
    [Trait("Category", "AdminConfigurationCleanup")]
    public void Legacy_reward_program_config_values_are_preserved_but_not_operational_ui()
    {
        var seed = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Application", "Provisioning", "TenantProvisioningDefaults.cs"));
        var snapshot = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Domain", "ValueObjects", "ProgramConfigSnapshot.cs"));

        Assert.Contains("LoyaltyConstants.ConfigKeys.RewardMiniProductPoints", seed);
        Assert.Contains("LoyaltyConstants.ConfigKeys.RewardFiftyOffPoints", seed);
        Assert.Contains("LoyaltyConstants.ConfigKeys.RewardFocusSkinPoints", seed);
        Assert.Contains("LoyaltyConstants.ConfigKeys.RewardMonthlyProductPoints", seed);
        Assert.Contains("LoyaltyConstants.ConfigKeys.RewardHundredOffCabinaPoints", seed);
        Assert.Contains("LoyaltyConstants.ConfigKeys.RewardFacialOffPoints", seed);
        Assert.Contains("RewardMonthlyProductPoints", snapshot);
        Assert.Contains("RewardCatalogItem.PointsCost", snapshot);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Admin_cookie_options_do_not_use_legacy_root_login_path()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Program.cs"));

        Assert.DoesNotContain("LoginPath = \"/login\"", source);
        Assert.DoesNotContain("AccessDeniedPath = \"/login\"", source);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    [Trait("Category", "TenantAdminAuth")]
    public void Tenant_admin_cookie_uses_configured_session_hours_and_persistent_data_protection()
    {
        var program = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "appsettings.json"));

        Assert.Contains("\"SessionHours\": 168", appsettings);
        Assert.Contains("GetSection(AdminAuthOptions.SectionName)", program);
        Assert.Contains("var adminSessionHours = Math.Max(1, adminAuthOptions.SessionHours);", program);
        Assert.Contains("options.ExpireTimeSpan = TimeSpan.FromHours(adminSessionHours);", program);
        Assert.DoesNotContain("options.ExpireTimeSpan = TimeSpan.FromHours(8);", program);
        Assert.Contains("options.SlidingExpiration = true;", program);
        Assert.DoesNotContain("AddDataProtection()", program);
        Assert.DoesNotContain("PersistKeysToFileSystem", program);
        Assert.DoesNotContain("data-protection-keys", program);
        Assert.Contains("\"SuperAdmin:SessionHours\") ?? 8", program);
        Assert.Contains("\"SuperAdmin\":", appsettings);
        Assert.Contains("\"SessionHours\": 8", appsettings);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Admin_visible_razor_text_uses_generic_customer_language()
    {
        var adminRoot = Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin");
        var razorFiles = Directory.EnumerateFiles(adminRoot, "*.razor", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var file in razorFiles)
        {
            var source = File.ReadAllText(file).ToLowerInvariant();
            Assert.DoesNotContain("clienta", source);
            Assert.DoesNotContain("clientas", source);
        }
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Admin_visible_text_uses_customer_id_instead_of_serial_wording()
    {
        var adminRoot = Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin");
        var visibleTextFiles = Directory.EnumerateFiles(adminRoot, "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(adminRoot, "wwwroot", "js"), "*.js", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var file in visibleTextFiles)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain(">Serial<", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Serial del cliente", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Buscar por serial", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("serial manualmente", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Ingresa un serial", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "Reports")]
    [Trait("Category", "AdminRouting")]
    public void Admin_menu_links_directly_to_useful_reports_without_exposing_the_reports_hub()
    {
        var root = GetRepositoryRoot();
        var layout = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Components", "Layout", "MainLayout.razor"));
        var reportsPage = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "Reports.razor"));
        var inactivePage = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "InactiveCustomersReport.razor"));
        var topRewardsPage = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "TopRewardsReport.razor"));

        Assert.Contains("<span class=\"kb-sidebar-section\">Reportes</span>", layout);
        Assert.DoesNotContain("href=\"/reports\"", layout);
        Assert.Contains("href=\"/reports/inactive-customers\"", layout);
        Assert.Contains("href=\"/reports/top-rewards\"", layout);
        Assert.Contains("@page \"/reports\"", reportsPage);
        Assert.Contains("@attribute [Authorize]", reportsPage);
        Assert.Contains("href=\"/customers\"", reportsPage);
        Assert.Contains("href=\"/redemptions\"", reportsPage);
        Assert.DoesNotContain("MetricCard", reportsPage);
        Assert.DoesNotContain("GetReportsSummaryQuery", reportsPage);
        Assert.Contains("@page \"/reports/inactive-customers\"", inactivePage);
        Assert.Contains("@page \"/reports/top-rewards\"", topRewardsPage);
        Assert.Contains("GetInactiveCustomersReportQuery", inactivePage);
        Assert.Contains("GetTopRewardsReportQuery", topRewardsPage);
        Assert.Contains("Sin actividad durante", inactivePage);
        Assert.Contains("Últimos 30 días", topRewardsPage);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Product_files_do_not_reference_retired_admin_hostname()
    {
        var root = GetRepositoryRoot();
        var retiredHost = "loyaltycloud-admin-" + "894839";
        var allowedApiHost = "loyaltycloud-api-" + "894839";
        var searchRoots = new[]
        {
            Path.Combine(root, "src"),
            Path.Combine(root, "docs"),
            Path.Combine(root, "scripts")
        };
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".razor",
            ".json",
            ".md",
            ".ps1",
            ".http",
            ".yml",
            ".yaml",
            ".props",
            ".targets",
            ".csproj"
        };

        var files = searchRoots
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            .Concat(new[]
            {
                Path.Combine(root, "README.md"),
                Path.Combine(root, "Directory.Build.props")
            }.Where(File.Exists))
            .Where(path => extensions.Contains(Path.GetExtension(path)))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain(retiredHost, source, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(
            allowedApiHost,
            File.ReadAllText(Path.Combine(root, "tests", "LoyaltyCloud.Tests", "Integration", "WalletProductionUpdateDiagnosticsTests.cs")),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src")))
            current = current.Parent;

        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static Dictionary<string, string> BuildLoginForm(string html, string username, string password)
    {
        var form = ExtractHiddenInputs(html);
        form["Input.Username"] = username;
        form["Input.Password"] = password;
        return form;
    }

    private static Dictionary<string, string> ExtractHiddenInputs(string html)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(html, "<input[^>]*>", RegexOptions.IgnoreCase))
        {
            var attributes = Regex.Matches(input.Value, "(?<name>[A-Za-z_:][-A-Za-z0-9_:.]*)=\"(?<value>[^\"]*)\"")
                .ToDictionary(
                    match => match.Groups["name"].Value,
                    match => WebUtility.HtmlDecode(match.Groups["value"].Value),
                    StringComparer.OrdinalIgnoreCase);

            if (!attributes.TryGetValue("type", out var type)
                || !string.Equals(type, "hidden", StringComparison.OrdinalIgnoreCase)
                || !attributes.TryGetValue("name", out var name))
            {
                continue;
            }

            values[name] = attributes.GetValueOrDefault("value") ?? string.Empty;
        }

        Assert.Contains(values, item => item.Key.Contains("RequestVerificationToken", StringComparison.OrdinalIgnoreCase));
        return values;
    }

    private static string ExtractCookies(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        return string.Join("; ", cookies.Select(cookie => cookie.Split(';', 2)[0]));
    }

    private static string ExtractCookie(HttpResponseMessage response, string cookieName)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var cookie = cookies
            .Select(value => value.Split(';', 2)[0])
            .FirstOrDefault(value => value.StartsWith(cookieName + "=", StringComparison.OrdinalIgnoreCase));

        Assert.False(string.IsNullOrWhiteSpace(cookie));
        return cookie!;
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
                    ["Admin:PublicBaseUrl"] = "https://admin.loyaltycloud.net",
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
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
            await SeedKBeautyPlatformRowsAsync(db);

            var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == TenantSeed.KBeautyTenantId);
            db.Entry(subscription).Property(nameof(TenantSubscription.PaidThroughUtc)).CurrentValue = DateTime.UtcNow.AddDays(30);

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

        private static async Task SeedKBeautyPlatformRowsAsync(AppDbContext db)
        {
            if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == TenantSeed.KBeautyTenantId))
            {
                var now = DateTime.UtcNow;
                db.Tenants.Add(new Tenant(
                    TenantSeed.KBeautyTenantId,
                    TenantSeed.KBeautySlug,
                    "KBeauty",
                    "America/Tijuana",
                    now));
                db.TenantBrandings.Add(new TenantBranding(
                    TenantSeed.KBeautyTenantId,
                    primaryColor: "#1C1C1C",
                    secondaryColor: "#E8668E"));
                db.TenantSubscriptions.Add(new TenantSubscription(
                    TenantSeed.KBeautyTenantId,
                    TenantSubscriptionStatus.Active,
                    "internal",
                    paidThroughUtc: DateTime.UtcNow.AddDays(30)));

                foreach (var row in TenantProvisioningDefaults.ProgramConfigRows)
                {
                    db.ProgramConfigs.Add(new ProgramConfig(
                        Guid.NewGuid(),
                        TenantSeed.KBeautyTenantId,
                        row.Key,
                        row.Value,
                        now,
                        row.Description,
                        TenantProvisioningDefaults.UpdatedBy));
                }

                await db.SaveChangesAsync();
            }
        }

        public async Task SetKBeautySubscriptionAsync(
            TenantSubscriptionStatus status,
            TenantSuspensionReason? suspensionReason)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var subscription = await db.TenantSubscriptions
                .SingleAsync(row => row.TenantId == TenantSeed.KBeautyTenantId);
            db.Entry(subscription).Property(nameof(TenantSubscription.Status)).CurrentValue = status;
            db.Entry(subscription).Property(nameof(TenantSubscription.SuspensionReason)).CurrentValue = suspensionReason;
            if (status == TenantSubscriptionStatus.Active)
            {
                db.Entry(subscription).Property(nameof(TenantSubscription.PaidThroughUtc)).CurrentValue =
                    DateTime.UtcNow.AddDays(30);
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
