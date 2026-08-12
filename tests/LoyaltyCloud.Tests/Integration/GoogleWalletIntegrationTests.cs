using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Common.Security;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class GoogleWalletIntegrationTests : IntegrationTestBase
{
    private const string SharedSecret = "test-admin-api-shared-secret-with-enough-length";
    private const string TenantSlug = "kbeauty";
    private const int WelcomeBonusPoints = 50;

    public GoogleWalletIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task SaveLink_ShouldCreateWalletIdempotently_AndAddPointsShouldSynchronizeObject()
    {
        var registerResponse = await Client.PostAsJsonAsync($"/api/public/{TenantSlug}/join", new
        {
            firstName = "Google",
            lastName = "Wallet",
            phone = "+52646" + Random.Shared.Next(1000000, 9999999)
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var registered = await registerResponse.Content.ReadFromJsonAsync<PublicJoinResponse>();
        Assert.NotNull(registered);
        var serial = registered!.SerialNumber;

        var saveLinkResponse = await Client.PostAsync(
            $"/api/customers/{serial}/wallets/google/save-link",
            content: null);
        Assert.Equal(HttpStatusCode.OK, saveLinkResponse.StatusCode);

        var saveLink = await saveLinkResponse.Content.ReadFromJsonAsync<GoogleWalletSaveLinkResponse>();
        Assert.NotNull(saveLink);
        Assert.StartsWith("https://pay.google.com/gp/v/save/", saveLink!.SaveUrl);
        Assert.StartsWith("issuer-test.", saveLink.ObjectId);
        Assert.Equal("issuer-test.loyalty", saveLink.ClassId);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var wallets = db.MemberDigitalWallets
                .IgnoreQueryFilters()
                .Where(w => w.Provider == DigitalWalletProvider.Google)
                .ToList();
            Assert.Single(wallets);
            Assert.Equal(saveLink.ObjectId, wallets[0].ExternalObjectId);
            Assert.NotNull(wallets[0].LastSynchronizedAt);
            Assert.NotNull(wallets[0].LastSaveLinkCreatedAt);
        }

        var secondSaveLinkResponse = await Client.PostAsync(
            $"/api/customers/{serial}/wallets/google/save-link",
            content: null);
        Assert.Equal(HttpStatusCode.OK, secondSaveLinkResponse.StatusCode);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, db.MemberDigitalWallets
                .IgnoreQueryFilters()
                .Count(w => w.Provider == DigitalWalletProvider.Google));
        }

        using var addPointsRequest = CreateSignedRequest(
            HttpMethod.Post,
            "/api/points",
            new { serialNumber = serial, purchaseAmount = 200m },
            operatorId: "google-wallet-test");
        var addPointsResponse = await Client.SendAsync(addPointsRequest);
        Assert.Equal(HttpStatusCode.OK, addPointsResponse.StatusCode);

        Assert.Contains(Factory.GoogleWallet.Objects, o =>
            o.Id == saveLink.ObjectId &&
            o.PointsBalance == WelcomeBonusPoints + 20 &&
            o.PointsText == "70 pts" &&
            o.NextLevelText == "Glow" &&
            o.RemainingPointsText == "930 pts");
    }

    private static HttpRequestMessage CreateSignedRequest(
        HttpMethod method,
        string path,
        object? body,
        string operatorId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var bodyBytes = body is null
            ? []
            : JsonSerializer.SerializeToUtf8Bytes(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var signature = AdminApiSignature.CreateSignature(
            SharedSecret,
            method.Method,
            path,
            timestamp,
            TenantSlug,
            operatorId,
            bodyBytes);

        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(bodyBytes);
            request.Content.Headers.ContentType = new("application/json");
        }

        request.Headers.Add(AdminApiSignature.TenantSlugHeader, TenantSlug);
        request.Headers.Add(AdminApiSignature.OperatorHeader, operatorId);
        request.Headers.Add(AdminApiSignature.TimestampHeader, timestamp);
        request.Headers.Add(AdminApiSignature.SignatureHeader, signature);
        return request;
    }

    private sealed record PublicJoinResponse(
        Guid CustomerId,
        string SerialNumber,
        string FullName,
        string Phone,
        bool AlreadyExists,
        string Message,
        string PassDownloadUrl);
}

