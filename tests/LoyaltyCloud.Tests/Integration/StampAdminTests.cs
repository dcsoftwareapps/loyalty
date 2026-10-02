extern alias AdminApp;

using System.Net;
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
        Assert.Contains("Sellos de bienvenida", html); Assert.Contains("Solo administradores", html);
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

    private static async Task<string> StampsCookieAsync(AdminRoutingTests.AdminWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var id = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(id, "stamp-admin");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        db.Add(new Tenant(id, "stamp-admin", "Stamps", "America/Tijuana", now, programType: LoyaltyProgramType.Stamps));
        db.Add(new TenantBranding(id, "#111111", "#999999"));
        db.Add(new TenantSubscription(id, TenantSubscriptionStatus.Active, "test", paidThroughUtc: now.AddDays(30)));
        var hashing = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
        db.Add(new TenantAdminUser(Guid.NewGuid(), id, "stamp-owner", hashing.HashPassword("Stamp123!"), now));
        await db.SaveChangesAsync();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Scheme = "https";
        var result = await scope.ServiceProvider.GetRequiredService<AdminAuthService>().TrySignInAsync(context, "stamp-admin", "stamp-owner", "Stamp123!");
        Assert.Equal(AdminLoginResult.Success, result);
        return context.Response.Headers.SetCookie.First(x => x!.StartsWith("loyaltycloud.admin.auth=", StringComparison.Ordinal))!.Split(';')[0];
    }
}
