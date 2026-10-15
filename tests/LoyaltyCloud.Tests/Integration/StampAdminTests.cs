extern alias AdminApp;

using System.Net;
using Microsoft.AspNetCore.Authorization;
using AdminApp::LoyaltyCloud.Admin.Auth;
using AdminApp::LoyaltyCloud.Admin.Services;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampAdmin")]
public sealed class StampAdminTests
{
    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/scan")]
    [InlineData("/redeem")]
    [InlineData("/redemptions")]
    [InlineData("/rewards")]
    [InlineData("/levels")]
    [InlineData("/campaigns")]
    [InlineData("/reports/inactive-customers")]
    [InlineData("/reports/top-rewards")]
    [InlineData("/reports/returning-customers")]
    [InlineData("/config?section=points")]
    public void Shared_route_guard_blocks_points_pages_for_stamps(string path)
    {
        Assert.False(AdminProgramNavigation.IsAllowed(LoyaltyProgramType.Stamps, path));
        Assert.True(AdminProgramNavigation.IsAllowed(LoyaltyProgramType.Points, path));
    }

    [Theory]
    [InlineData("/customers")]
    [InlineData("/customers/TEST")]
    [InlineData("/marketing-notifications")]
    [InlineData("/giftcards")]
    [InlineData("/giftcards/reports")]
    [InlineData("/config")]
    [InlineData("/config?section=giftcards")]
    [InlineData("/tenant/billing")]
    [InlineData("/staff")]
    public void Common_features_remain_available(string path) => Assert.True(AdminProgramNavigation.IsAllowed(LoyaltyProgramType.Stamps, path));

    [Fact]
    public async Task Stamps_navigation_and_initial_configuration_render_without_points_options()
    {
        await using var factory = new AdminRoutingTests.AdminWebApplicationFactory();
        await factory.EnsureDatabaseCreatedAsync();
        var cookie = await StampsCookieAsync(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var response = await client.GetAsync("/stamps");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("href=\"/stamps\"", html);
        Assert.Contains("Configura tu programa de sellos", html);
        Assert.Contains("Sellos de bienvenida", html); Assert.Contains("Administradores", html);
        Assert.Contains("Apariencia de la tarjeta", html);
        Assert.Contains("Sello vacío", html); Assert.Contains("Sello lleno", html);
        Assert.Contains("Cambiar imagen", html); Assert.Contains("Usar predeterminada", html);
        Assert.Contains("data:image/png;base64,", html);
        Assert.Contains("href=\"/customers\"", html); Assert.Contains("href=\"/marketing-notifications\"", html);
        Assert.Contains("href=\"/config\"", html); Assert.Contains("href=\"/staff\"", html);
        foreach (var route in new[] { "/dashboard", "/scan", "/redeem", "/rewards", "/levels", "/campaigns", "/reports/activity-trends" })
            Assert.DoesNotContain($"href=\"{route}\"", html);
        using var denied = await client.GetAsync("/levels");
        Assert.Contains("Programa no disponible", await denied.Content.ReadAsStringAsync());
        using var config = await client.GetAsync("/config");
        var configHtml = await config.Content.ReadAsStringAsync();
        Assert.DoesNotContain("href=\"/config?section=points\"", configHtml);
        Assert.DoesNotContain("kb-wallet-preview-field-labels", configHtml);
        Assert.Contains("Color de tarjeta", configHtml);
    }

    [Fact]
    public async Task Points_navigation_is_preserved_and_stamps_url_is_blocked()
    {
        await using var factory = new AdminRoutingTests.AdminWebApplicationFactory();
        await factory.EnsureDatabaseCreatedAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", await factory.CreateTenantAdminCookieAsync());
        using var response = await client.GetAsync("/stamps");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Programa no disponible", html);
        foreach (var route in new[] { "/rewards", "/levels", "/campaigns", "/scan", "/redeem" })
            Assert.Contains($"href=\"{route}\"", html);
        Assert.DoesNotContain("href=\"/stamps\"", html);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Stamps_sidebar_order_and_gift_card_conditions(bool gifts)
    {
        await using var factory = new AdminRoutingTests.AdminWebApplicationFactory(); await factory.EnsureDatabaseCreatedAsync();
        var cookie = await StampsCookieAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var tenant = scope.ServiceProvider.GetRequiredService<IMutableTenantContext>();
            // Resolve the seeded Stamps tenant without relying on the HTTP request scope.
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var id = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Tenants.Where(x => x.Slug == "stamp-admin").Select(x => x.Id));
            tenant.SetTenant(id, "stamp-admin");
            await scope.ServiceProvider.GetRequiredService<LoyaltyCloud.Application.GiftCards.IGiftCardService>().SetEnabledAsync(gifts);
        }
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("Cookie", cookie);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/stamps"));
        var nav = html[html.IndexOf("<nav ", StringComparison.Ordinal)..html.IndexOf("</nav>", StringComparison.Ordinal)];
        var order = new[] { "Programa", "href=\"/cashier\"", "href=\"/marketing-notifications\"", "Reportes", "href=\"/customers\"", "Configuración", "href=\"/stamps\"", "href=\"/config\"", "href=\"/stamp-admin/billing\"", "href=\"/staff\"", "href=\"/quick-help\"" };
        var positions = order.Select(x => nav.IndexOf(x, StringComparison.Ordinal)).ToArray();
        Assert.All(positions, x => Assert.True(x >= 0)); Assert.Equal(positions.OrderBy(x => x), positions);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(nav, "href=\"/cashier\"").Cast<System.Text.RegularExpressions.Match>());
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(nav, "href=\"/stamps\"").Cast<System.Text.RegularExpressions.Match>());
        Assert.Contains("Operaciones", nav); Assert.DoesNotContain("Gestión", nav);
        foreach (var route in new[] { "/giftcards", "/giftcards/issue", "/giftcards/redeem", "/giftcards/cards", "/giftcards/reports" })
            Assert.Equal(gifts, nav.Contains($"href=\"{route}\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Admin", true, true)] [InlineData("Cashier", true, true)] [InlineData("Viewer", true, false)] [InlineData("Admin", false, false)]
    public async Task Operations_policy_preserves_existing_roles(string role, bool authenticated, bool allowed)
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection(); services.AddLogging(); services.AddAuthorization(TenantAuthorization.Configure);
        await using var provider = services.BuildServiceProvider();
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role)], authenticated ? "test" : null));
        var result = await provider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>()
            .AuthorizeAsync(principal, null, TenantAuthorizationPolicies.CashierOperations);
        Assert.Equal(allowed, result.Succeeded);
    }

    [Theory]
    [InlineData(LoyaltyProgramType.Stamps, TenantUserRole.Admin)]
    [InlineData(LoyaltyProgramType.Stamps, TenantUserRole.Cashier)]
    [InlineData(LoyaltyProgramType.Points, TenantUserRole.Admin)]
    [InlineData(LoyaltyProgramType.Points, TenantUserRole.Cashier)]
    public async Task Operations_layout_follows_authenticated_role_without_changing_access(LoyaltyProgramType program, TenantUserRole role)
    {
        await using var factory = new AdminRoutingTests.AdminWebApplicationFactory(); await factory.EnsureDatabaseCreatedAsync();
        var cookie = await StampsCookieAsync(factory, program, role);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var response = await client.GetAsync("/cashier"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(role == TenantUserRole.Admin, html.Contains("<aside class=\"kb-sidebar\"", StringComparison.Ordinal));
        Assert.Contains("kb-cashier-shell", html);
        Assert.Equal(role == TenantUserRole.Cashier, html.Contains("class=\"kb-cashier-header\"", StringComparison.Ordinal));
        if (role == TenantUserRole.Cashier) { Assert.Contains("stamp-owner", html); Assert.Contains("Cerrar sesión", html); }
        Assert.Contains(program == LoyaltyProgramType.Stamps ? "Sellos" : "Puntos", html);
        if (role == TenantUserRole.Cashier)
        {
            Assert.DoesNotContain("href=\"/staff\"", html);
            using var scope = factory.Services.CreateScope();
            var authContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider }; authContext.Request.Scheme = "https";
            await scope.ServiceProvider.GetRequiredService<AdminAuthService>().TrySignInAsync(authContext, "stamp-admin", "stamp-owner", "Stamp123!");
            var access = await scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>()
                .AuthorizeAsync(authContext.User, null, TenantAuthorizationPolicies.TenantAdmin);
            Assert.False(access.Succeeded);
        }
    }

    [Theory]
    [InlineData(LoyaltyProgramType.Stamps)] [InlineData(LoyaltyProgramType.Points)]
    public async Task Help_copy_matches_program_and_preserves_registration_poster(LoyaltyProgramType program)
    {
        await using var factory = new AdminRoutingTests.AdminWebApplicationFactory(); await factory.EnsureDatabaseCreatedAsync();
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("Cookie", await StampsCookieAsync(factory, program));
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/quick-help"));
        Assert.Contains(program == LoyaltyProgramType.Stamps ? "Regístrate y empieza a acumular sellos" : "Regístrate y empieza a acumular puntos", html);
        Assert.Contains("QR de registro", html); Assert.Contains("/stamp-admin/join", html); Assert.Contains("Powered by LoyaltyCloud", html);
        if (program == LoyaltyProgramType.Stamps)
        {
            Assert.DoesNotContain("acumular puntos", html); Assert.DoesNotContain("Captura el monto de la compra", html);
            Assert.DoesNotContain("Descuento en dinero", html); Assert.DoesNotContain("campaña aplicable", html);
            Assert.Contains("Entregar una recompensa", html); Assert.Contains("Escanear agrega un sello", html);
        }
        else { Assert.Contains("Captura el monto de la compra", html); Assert.Contains("Descuento en dinero", html); }
    }

    [Theory]
    [InlineData(LoyaltyProgramType.Stamps, TenantUserRole.Admin, "/cashier")]
    [InlineData(LoyaltyProgramType.Points, TenantUserRole.Admin, "/dashboard")]
    [InlineData(LoyaltyProgramType.Stamps, TenantUserRole.Cashier, "/cashier")]
    [InlineData(LoyaltyProgramType.Points, TenantUserRole.Cashier, "/cashier")]
    public async Task Login_destinations_use_principal_program_and_keep_billing_and_return_url_rules(LoyaltyProgramType program, TenantUserRole role, string landing)
    {
        await using var factory = new AdminRoutingTests.AdminWebApplicationFactory(); await factory.EnsureDatabaseCreatedAsync();
        var cookie = await StampsCookieAsync(factory, program, role);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var login = await client.GetAsync("/stamp-admin/login"); Assert.Equal(landing, login.Headers.Location?.OriginalString);
        using var scope = factory.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<AdminAuthService>(); var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider }; context.Request.Scheme = "https";
        Assert.Equal(AdminLoginResult.Success, await auth.TrySignInAsync(context, "stamp-admin", "stamp-owner", "Stamp123!"));
        Assert.Equal(landing, await auth.GetAuthenticatedLandingPathAsync(context.User));
        Assert.Equal(landing, await auth.GetPostLoginDestinationAsync(context, "stamp-admin", false, null));
        Assert.Equal(landing, await auth.GetPostLoginDestinationAsync(context, "stamp-admin", false, "//external.invalid"));
        Assert.Equal(role == TenantUserRole.Cashier ? "/cashier" : "/stamp-admin/billing", await auth.GetPostLoginDestinationAsync(context, "stamp-admin", true, null));
        Assert.Equal(role == TenantUserRole.Cashier ? "/cashier" : "/customers", await auth.GetPostLoginDestinationAsync(context, "stamp-admin", false, "/customers"));
        foreach (var path in new[] { "/dashboard", "/levels", "/reports/activity-trends", "/config?section=points" })
            Assert.Equal(role == TenantUserRole.Cashier || program == LoyaltyProgramType.Stamps ? "/cashier" : path,
                await auth.GetPostLoginDestinationAsync(context, "stamp-admin", false, path));
        if (role == TenantUserRole.Admin && program == LoyaltyProgramType.Stamps)
        {
            var denied = WebUtility.HtmlDecode(await client.GetStringAsync("/dashboard"));
            Assert.Contains("Programa no disponible", denied); Assert.Contains("href=\"/cashier\">Volver al programa", denied);
        }
    }

    [Fact]
    public async Task Reward_form_has_one_shared_short_name_explanation_and_top_aligned_rows()
    {
        await using var factory = new AdminRoutingTests.AdminWebApplicationFactory(); await factory.EnsureDatabaseCreatedAsync();
        var cookie = await StampsCookieAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var id = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Tenants.Where(x => x.Slug == "stamp-admin").Select(x => x.Id));
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(id, "stamp-admin");
            var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.TenantAdminUsers);
            var version = new StampProgramVersion(Guid.NewGuid(), id, 1, 10, 0, StampReversalPolicy.None, user.Id, DateTime.UtcNow); db.Add(version);
            foreach (var threshold in new[] { 5, 10 })
            {
                var identity = new StampRewardIdentity(Guid.NewGuid(), id); db.Add(identity);
                db.Add(new StampRewardDefinition(Guid.NewGuid(), version, threshold, "Recompensa", null, identity.Id, "Premio"));
            }
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("Cookie", cookie);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/stamps"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "El nombre corto se usa en las notificaciones de Wallet").Cast<System.Text.RegularExpressions.Match>());
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "aria-describedby=\"stamp-short-help\"").Count);
        var css = await client.GetStringAsync("/css/site.css");
        var row = System.Text.RegularExpressions.Regex.Match(css, @"\.kb-stamp-reward\s*\{[^}]+\}").Value;
        Assert.Contains("align-items: start", row); Assert.Contains("40px", row); Assert.Contains(".kb-stamp-reward .kb-field", css);
    }

    [Theory]
    [InlineData(LoyaltyProgramType.Stamps)] [InlineData(LoyaltyProgramType.Points)]
    public async Task Customer_columns_and_detail_link_match_program(LoyaltyProgramType program)
    {
        await using var factory = new AdminRoutingTests.AdminWebApplicationFactory(); await factory.EnsureDatabaseCreatedAsync();
        var cookie = await StampsCookieAsync(factory, program);
        Guid customerId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Tenants.Where(t => t.Slug == "stamp-admin").Select(t => t.Id));
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, "stamp-admin");
            customerId = Guid.NewGuid(); var now = DateTime.UtcNow;
            db.Add(new Customer(customerId, tenantId, "Daniel Sellos", "daniel@example.test", Customer.BirthdayNotCaptured, now));
            db.Add(new LoyaltyCard(Guid.NewGuid(), tenantId, customerId, "KB-QA-CLIENT", now)); await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("Cookie", cookie);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/customers"));
        Assert.Contains("Daniel Sellos", html); Assert.Contains("KB-QA-CLIENT", html); Assert.Contains("<th>Alta</th>", html);
        Assert.Equal(program == LoyaltyProgramType.Points, html.Contains("<th>Nivel</th>", StringComparison.Ordinal));
        Assert.Equal(program == LoyaltyProgramType.Points, html.Contains("<th>Puntos</th>", StringComparison.Ordinal));
        Assert.Equal(program == LoyaltyProgramType.Points, html.Contains("Ver detalle", StringComparison.Ordinal));
        Assert.Equal(program == LoyaltyProgramType.Points, html.Contains("id=\"level\"", StringComparison.Ordinal));
        Assert.Equal(program == LoyaltyProgramType.Points, html.Contains($"href=\"/customers/{customerId}\"", StringComparison.Ordinal));
    }

    private static async Task<string> StampsCookieAsync(AdminRoutingTests.AdminWebApplicationFactory factory, LoyaltyProgramType program = LoyaltyProgramType.Stamps, TenantUserRole role = TenantUserRole.Admin)
    {
        using var scope = factory.Services.CreateScope();
        var id = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(id, "stamp-admin");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        db.Add(new Tenant(id, "stamp-admin", "Stamps", "America/Tijuana", now, programType: program));
        db.Add(new TenantBranding(id, "#111111", "#999999"));
        db.Add(new TenantSubscription(id, TenantSubscriptionStatus.Active, "test", paidThroughUtc: now.AddDays(30)));
        var hashing = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
        db.Add(new TenantAdminUser(Guid.NewGuid(), id, "stamp-owner", hashing.HashPassword("Stamp123!"), now, role: role));
        await db.SaveChangesAsync();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Scheme = "https";
        var result = await scope.ServiceProvider.GetRequiredService<AdminAuthService>().TrySignInAsync(context, "stamp-admin", "stamp-owner", "Stamp123!");
        Assert.Equal(AdminLoginResult.Success, result);
        return context.Response.Headers.SetCookie.First(x => x!.StartsWith("loyaltycloud.admin.auth=", StringComparison.Ordinal))!.Split(';')[0];
    }
}
