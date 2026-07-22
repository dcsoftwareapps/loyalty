using System.Net;
using System.Net.Http.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class PublicJoinTenantRoutingTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private static readonly Guid BellaTenantId = Guid.Parse("b3000000-0000-0000-0000-000000000001");
    private const string BellaSlug = "bella-salon";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PublicJoinTenantRoutingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        await EnsureBellaTenantAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "PublicJoin")]
    public async Task KBeauty_slug_registers_customer_in_kbeauty_tenant()
    {
        var phone = UniquePhone();

        var response = await JoinAsync("kbeauty", "Karla", "Lopez", phone);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var customerTenantId = await FindCustomerTenantByPhoneAsync(phone);
        Assert.Equal(TenantSeed.KBeautyTenantId, customerTenantId);
    }

    [Fact]
    [Trait("Category", "PublicJoin")]
    public async Task Bella_slug_registers_customer_in_bella_tenant()
    {
        var phone = UniquePhone();

        var response = await JoinAsync(BellaSlug, "Bella", "Cliente", phone);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var customerTenantId = await FindCustomerTenantByPhoneAsync(phone);
        Assert.Equal(BellaTenantId, customerTenantId);
    }

    [Fact]
    [Trait("Category", "PublicJoin")]
    public async Task Unknown_slug_is_blocked()
    {
        var response = await JoinAsync("tenant-inexistente", "Ana", "Prueba", UniquePhone());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Programa de lealtad no encontrado.", body);
    }

    [Fact]
    [Trait("Category", "PublicJoin")]
    public async Task Suspended_tenant_is_blocked()
    {
        await SetBellaSubscriptionStatusAsync(TenantSubscriptionStatus.Suspended);

        var response = await JoinAsync(BellaSlug, "Ana", "Suspendida", UniquePhone());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Este programa de lealtad no est", body);
    }

    [Fact]
    [Trait("Category", "PublicJoin")]
    public async Task Same_phone_can_join_two_different_tenants()
    {
        var phone = UniquePhone();

        var kbeauty = await JoinAsync("kbeauty", "Doble", "KBeauty", phone);
        var bella = await JoinAsync(BellaSlug, "Doble", "Bella", phone);

        Assert.Equal(HttpStatusCode.OK, kbeauty.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bella.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenants = await db.Customers
            .IgnoreQueryFilters()
            .Where(c => c.NormalizedPhone == phone)
            .Select(c => c.TenantId)
            .OrderBy(id => id)
            .ToListAsync();

        Assert.Contains(TenantSeed.KBeautyTenantId, tenants);
        Assert.Contains(BellaTenantId, tenants);
    }

    [Fact]
    [Trait("Category", "PublicJoin")]
    public void Legacy_join_route_redirects_to_kbeauty_join()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "LoyaltyCloud.Admin",
            "Pages",
            "Join.razor"));

        Assert.Contains("@page \"/join\"", source);
        Assert.Contains("Navigation.NavigateTo($\"/{LegacyDefaultTenantSlug}/join\", replace: true);", source);
        Assert.Contains("LegacyDefaultTenantSlug = \"kbeauty\"", source);
    }

    private async Task<HttpResponseMessage> JoinAsync(string slug, string firstName, string lastName, string phone) =>
        await _client.PostAsJsonAsync($"api/public/{slug}/join", new
        {
            firstName,
            lastName,
            phone
        });

    private async Task<Guid?> FindCustomerTenantByPhoneAsync(string phone)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Customers
            .IgnoreQueryFilters()
            .Where(c => c.NormalizedPhone == phone)
            .Select(c => (Guid?)c.TenantId)
            .SingleOrDefaultAsync();
    }

    private async Task EnsureBellaTenantAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Tenants.AnyAsync(t => t.Id == BellaTenantId))
        {
            db.Tenants.Add(new Tenant(
                BellaTenantId,
                BellaSlug,
                "Bella Salon",
                "America/Tijuana",
                DateTime.UtcNow));
            db.TenantBrandings.Add(new TenantBranding(
                BellaTenantId,
                primaryColor: "#8B5CF6",
                secondaryColor: "#F5D0FE"));
            db.TenantSubscriptions.Add(new TenantSubscription(
                BellaTenantId,
                TenantSubscriptionStatus.Active,
                "test"));
            await db.SaveChangesAsync();
        }

        await SetBellaSubscriptionStatusAsync(TenantSubscriptionStatus.Active);
    }

    private async Task SetBellaSubscriptionStatusAsync(TenantSubscriptionStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == BellaTenantId);
        db.Entry(subscription).Property(nameof(TenantSubscription.Status)).CurrentValue = status;
        await db.SaveChangesAsync();
    }

    private static string UniquePhone() => "646" + Random.Shared.Next(1000000, 9999999).ToString();
}
