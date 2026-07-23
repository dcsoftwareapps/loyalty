using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
using LoyaltyCloud.Common.Security;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class AdminApiPointsFlowTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private const string SharedSecret = "test-admin-api-shared-secret-with-enough-length";
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminApiPointsFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureDatabaseCreatedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "WalletProductionUpdate")]
    public async Task Add_points_api_rejects_unsigned_admin_request()
    {
        using var response = await _client.PostAsJsonAsync("/api/points", new
        {
            serialNumber = "KB-NOSIGN",
            purchaseAmount = 100m
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "WalletProductionUpdate")]
    public async Task Signed_admin_points_request_runs_in_api_and_attempts_wallet_push()
    {
        var serial = "KB-APNAPI1";
        await SeedCardWithDeviceAsync(serial);
        var initialApnCount = _factory.Apn.Calls.Count;

        using var request = CreateSignedAddPointsRequest(serial, 100m);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AddPointsResponse>();
        Assert.NotNull(result);
        Assert.Equal(10, result!.PointsAdded);
        Assert.True(_factory.Apn.Calls.Count > initialApnCount);
        Assert.Contains(_factory.Apn.Calls, call => call.Token == "push-token-api-flow");
    }

    private async Task SeedCardWithDeviceAsync(string serial)
    {
        using var scope = _factory.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<IMutableTenantContext>();
        tenantContext.SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == TenantSeed.KBeautyTenantId);
        db.Entry(subscription).Property(nameof(TenantSubscription.PaidThroughUtc)).CurrentValue = DateTime.UtcNow.AddDays(30);

        if (await db.LoyaltyCards.AnyAsync(c => c.SerialNumber == serial))
        {
            await db.SaveChangesAsync();
            return;
        }

        var now = DateTime.UtcNow;
        var customer = new Customer(
            Guid.NewGuid(),
            TenantSeed.KBeautyTenantId,
            "Wallet API Customer",
            $"wallet-api-{Guid.NewGuid():N}@test.local",
            new DateTime(1990, 1, 1),
            now,
            "6460000000");
        var card = new LoyaltyCard(
            Guid.NewGuid(),
            TenantSeed.KBeautyTenantId,
            customer.Id,
            serial,
            now);

        db.Customers.Add(customer);
        db.LoyaltyCards.Add(card);
        db.DeviceRegistrations.Add(new DeviceRegistration(
            Guid.NewGuid(),
            TenantSeed.KBeautyTenantId,
            "device-api-flow",
            "pass.com.kbeautymx.loyalty",
            serial,
            "push-token-api-flow",
            now));
        await db.SaveChangesAsync();
    }

    private static HttpRequestMessage CreateSignedAddPointsRequest(string serial, decimal purchaseAmount)
    {
        const string path = "/api/points";
        const string tenantSlug = "kbeauty";
        const string operatorId = "admin-api-test";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var body = JsonSerializer.SerializeToUtf8Bytes(
            new { serialNumber = serial, purchaseAmount },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var signature = AdminApiSignature.CreateSignature(
            SharedSecret,
            HttpMethod.Post.Method,
            path,
            timestamp,
            tenantSlug,
            operatorId,
            body);

        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add(AdminApiSignature.TenantSlugHeader, tenantSlug);
        request.Headers.Add(AdminApiSignature.OperatorHeader, operatorId);
        request.Headers.Add(AdminApiSignature.TimestampHeader, timestamp);
        request.Headers.Add(AdminApiSignature.SignatureHeader, signature);
        return request;
    }
}
