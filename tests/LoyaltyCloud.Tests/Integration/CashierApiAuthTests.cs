using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using LoyaltyCloud.API.Auth;
using LoyaltyCloud.API.Controllers;
using LoyaltyCloud.API.Middleware;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Security;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class CashierApiAuthTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private const string CashierPassword = "CashierAuth123!";
    private const string AdminPassword = "AdminAuth123!";
    private const string CashierUsername = "mobile-cashier";
    private const string AdminUsername = "tenant-admin-api";
    private const string SharedSecret = "test-admin-api-shared-secret-with-enough-length";
    private static readonly Guid CashierUserId = Guid.Parse("ac000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("ac000000-0000-0000-0000-000000000002");
    private static readonly Guid BellaTenantId = Guid.Parse("ac000000-0000-0000-0000-000000000101");
    private static readonly Guid BellaCashierUserId = Guid.Parse("ac000000-0000-0000-0000-000000000102");
    private const string BellaSlug = "bella-cashier";
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CashierApiAuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        await SeedCashierUsersAsync();
        await SeedCardsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "CashierAuth")]
    public async Task Valid_admin_credentials_can_authenticate_for_cashier_api()
    {
        var response = await LoginAsync(AdminUsername, AdminPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CashierAuthController.CashierLoginResponse>();
        Assert.NotNull(body);
        Assert.Equal("Bearer", body!.TokenType);
        Assert.Equal(TenantSeed.KBeautySlug, body.TenantSlug);
        Assert.Equal(AdminUserId, body.UserId);
        Assert.Equal(AdminUsername, body.Username);
        Assert.Equal(TenantUserRole.Admin.ToString(), body.Role);
        Assert.DoesNotContain("SigningKey", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "CashierAuth")]
    public async Task Valid_cashier_credentials_can_authenticate()
    {
        var response = await LoginAsync(CashierUsername, CashierPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CashierAuthController.CashierLoginResponse>();
        Assert.NotNull(body);
        Assert.Equal(CashierUserId, body!.UserId);
        Assert.Equal(TenantUserRole.Cashier.ToString(), body.Role);
    }

    [Fact]
    [Trait("Category", "CashierAuth")]
    public async Task Invalid_password_and_wrong_tenant_are_rejected()
    {
        using var invalidPassword = await LoginAsync(CashierUsername, "wrong-password");
        using var wrongTenant = await LoginAsync(AdminUsername, AdminPassword, tenantSlug: BellaSlug);

        Assert.Equal(HttpStatusCode.Unauthorized, invalidPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongTenant.StatusCode);
    }

    [Fact]
    [Trait("Category", "CashierAuth")]
    public async Task Token_contains_tenant_user_and_role_claims()
    {
        var token = await LoginAndGetTokenAsync(CashierUsername, CashierPassword);

        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<CashierAccessTokenService>();
        var result = tokens.ValidateToken(token);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.Equal(CashierUserId.ToString(), result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(TenantSeed.KBeautyTenantId.ToString(), result.Principal.FindFirstValue(CashierClaimTypes.TenantId));
        Assert.Equal(TenantSeed.KBeautySlug, result.Principal.FindFirstValue(CashierClaimTypes.TenantSlug));
        Assert.Equal(TenantUserRole.Cashier.ToString(), result.Principal.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    [Trait("Category", "CashierAuth")]
    public async Task Cashier_bearer_can_use_operational_endpoints_and_operator_id_comes_from_token()
    {
        var token = await LoginAndGetTokenAsync(CashierUsername, CashierPassword);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/points");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CashierAuthDefaults.OperatorIdHeader, "spoofed-operator");
        request.Content = JsonContent.Create(new { serialNumber = "KB-CASHIER1", purchaseAmount = 100m });

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AddPointsResponse>();
        Assert.NotNull(result);
        Assert.Equal(10, result!.PointsAdded);

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
            .SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transaction = await db.PointTransactions
            .OrderByDescending(t => t.CreatedAt)
            .FirstAsync(t => t.TenantId == TenantSeed.KBeautyTenantId);
        Assert.Equal(CashierUserId.ToString(), transaction.CreatedBy);
        Assert.NotEqual("spoofed-operator", transaction.CreatedBy);
    }

    [Fact]
    [Trait("Category", "CashierAuth")]
    public async Task Admin_bearer_can_use_cashier_operations()
    {
        var token = await LoginAndGetTokenAsync(AdminUsername, AdminPassword);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/customers/KB-CASHIER1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "CashierAuth")]
    public async Task Cashier_bearer_cannot_access_admin_only_config_endpoint()
    {
        var token = await LoginAndGetTokenAsync(CashierUsername, CashierPassword);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/config");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "CashierAuth")]
    public async Task Anonymous_invalid_and_expired_tokens_are_rejected()
    {
        using var anonymous = await _client.GetAsync("/api/customers/KB-CASHIER1");
        using var invalidRequest = new HttpRequestMessage(HttpMethod.Get, "/api/customers/KB-CASHIER1");
        invalidRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        using var invalid = await _client.SendAsync(invalidRequest);

        var token = await LoginAndGetTokenAsync(CashierUsername, CashierPassword);
        var expiredToken = CreateExpiredToken(token);
        using var expiredRequest = new HttpRequestMessage(HttpMethod.Get, "/api/customers/KB-CASHIER1");
        expiredRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        using var expired = await _client.SendAsync(expiredRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [Fact]
    [Trait("Category", "CashierAuth")]
    public async Task Cashier_from_tenant_a_cannot_query_tenant_b_customer()
    {
        var token = await LoginAndGetTokenAsync(CashierUsername, CashierPassword);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/customers/KB-BELLCASH");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "CashierAuth")]
    public async Task Existing_admin_hmac_authentication_still_works_for_operational_endpoints()
    {
        using var request = CreateSignedRequest(HttpMethod.Get, "/api/customers/KB-CASHIER1", null);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "CashierAuth")]
    public void Static_policy_definitions_keep_cashier_out_of_admin_only_policy()
    {
        var config = new DefaultHttpContext();
        config.Request.Method = HttpMethods.Get;
        config.Request.Path = "/api/config";
        Assert.True(AdminApiAuthenticationMiddleware.RequiresAdminApiAuthentication(config.Request));
        Assert.False(AdminApiAuthenticationMiddleware.AllowsCashierBearerAuthentication(config.Request));

        var points = new DefaultHttpContext();
        points.Request.Method = HttpMethods.Post;
        points.Request.Path = "/api/points";
        Assert.False(AdminApiAuthenticationMiddleware.RequiresAdminApiAuthentication(points.Request));
        Assert.True(AdminApiAuthenticationMiddleware.AllowsCashierBearerAuthentication(points.Request));
    }

    private async Task<HttpResponseMessage> LoginAsync(
        string username,
        string password,
        string tenantSlug = TenantSeed.KBeautySlug) =>
        await _client.PostAsJsonAsync("/api/auth/cashier/login", new
        {
            tenantSlug,
            username,
            password
        });

    private async Task<string> LoginAndGetTokenAsync(string username, string password, string tenantSlug = TenantSeed.KBeautySlug)
    {
        using var response = await LoginAsync(username, password, tenantSlug);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CashierAuthController.CashierLoginResponse>();
        Assert.NotNull(body);
        return body!.AccessToken;
    }

    private string CreateExpiredToken(string validToken)
    {
        var parts = validToken.Split('.');
        var payload = JsonSerializer.Deserialize<CashierTokenPayload>(WebEncoders.Base64UrlDecode(parts[1]))!;
        var expiredPayload = payload with
        {
            IssuedAt = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds(),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds()
        };
        var payloadJson = JsonSerializer.SerializeToUtf8Bytes(expiredPayload);
        var encodedPayload = WebEncoders.Base64UrlEncode(payloadJson);
        var signingKey = _factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["CashierAuth:SigningKey"]!;
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(signingKey));
        var signature = WebEncoders.Base64UrlEncode(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(encodedPayload)));
        return $"v1.{encodedPayload}.{signature}";
    }

    private async Task SeedCashierUsersAsync()
    {
        {
            using var scope = _factory.Services.CreateScope();
            var passwords = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
                .SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);

            if (!await db.TenantAdminUsers.AnyAsync(u => u.Id == CashierUserId))
            {
                db.TenantAdminUsers.Add(new TenantAdminUser(
                    CashierUserId,
                    TenantSeed.KBeautyTenantId,
                    CashierUsername,
                    passwords.HashPassword(CashierPassword),
                    DateTime.UtcNow,
                    role: TenantUserRole.Cashier));
            }

            if (!await db.TenantAdminUsers.AnyAsync(u => u.Id == AdminUserId))
            {
                db.TenantAdminUsers.Add(new TenantAdminUser(
                    AdminUserId,
                    TenantSeed.KBeautyTenantId,
                    AdminUsername,
                    passwords.HashPassword(AdminPassword),
                    DateTime.UtcNow,
                    role: TenantUserRole.Admin));
            }

            await db.SaveChangesAsync();
        }

        {
            using var scope = _factory.Services.CreateScope();
            var passwords = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
                .SetTenant(BellaTenantId, BellaSlug);

            if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == BellaTenantId))
            {
                db.Tenants.Add(new Tenant(BellaTenantId, BellaSlug, "Bella Cashier", "America/Tijuana", DateTime.UtcNow));
                db.TenantBrandings.Add(new TenantBranding(BellaTenantId, "#111111", "#eeeeee"));
                db.TenantSubscriptions.Add(new TenantSubscription(
                    BellaTenantId,
                    TenantSubscriptionStatus.Active,
                    "internal",
                    paidThroughUtc: DateTime.UtcNow.AddDays(30)));
                await db.SaveChangesAsync();
            }

            if (!await db.TenantAdminUsers.AnyAsync(u => u.Id == BellaCashierUserId))
            {
                db.TenantAdminUsers.Add(new TenantAdminUser(
                    BellaCashierUserId,
                    BellaTenantId,
                    CashierUsername,
                    passwords.HashPassword(CashierPassword),
                    DateTime.UtcNow,
                    role: TenantUserRole.Cashier));
            }

            await db.SaveChangesAsync();
            await IntegrationTestSeed.EnsureProgramConfigAsync(db, BellaTenantId);
            await IntegrationTestSeed.EnsureDefaultTenantLevelsAsync(db, BellaTenantId);
        }
    }

    private async Task SeedCardsAsync()
    {
        await SeedCardAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, "KB-CASHIER1", "Cashier Customer");
        await SeedCardAsync(BellaTenantId, BellaSlug, "KB-BELLCASH", "Bella Customer");
    }

    private async Task SeedCardAsync(Guid tenantId, string tenantSlug, string serial, string name)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, tenantSlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.LoyaltyCards.AnyAsync(c => c.SerialNumber == serial))
            return;

        var customer = new Customer(
            Guid.NewGuid(),
            tenantId,
            name,
            $"{serial.ToLowerInvariant()}@test.local",
            new DateTime(1990, 1, 1),
            DateTime.UtcNow,
            $"646{Random.Shared.Next(1000000, 9999999)}");
        db.Customers.Add(customer);
        db.LoyaltyCards.Add(new LoyaltyCard(
            Guid.NewGuid(),
            tenantId,
            customer.Id,
            serial,
            DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    private static HttpRequestMessage CreateSignedRequest(
        HttpMethod method,
        string path,
        object? body,
        string tenantSlug = TenantSeed.KBeautySlug)
    {
        const string operatorId = "admin-api-test";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var bodyBytes = body is null
            ? []
            : JsonSerializer.SerializeToUtf8Bytes(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var signature = AdminApiSignature.CreateSignature(
            SharedSecret,
            method.Method,
            path,
            timestamp,
            tenantSlug,
            operatorId,
            bodyBytes);

        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(bodyBytes);
            request.Content.Headers.ContentType = new("application/json");
        }

        request.Headers.Add(AdminApiSignature.TenantSlugHeader, tenantSlug);
        request.Headers.Add(AdminApiSignature.OperatorHeader, operatorId);
        request.Headers.Add(AdminApiSignature.TimestampHeader, timestamp);
        request.Headers.Add(AdminApiSignature.SignatureHeader, signature);
        return request;
    }
}
