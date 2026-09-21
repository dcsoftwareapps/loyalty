using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GoogleWalletNotificationClientTests
{
    [Fact]
    public async Task ExistingLoyaltyClass_PatchesNativeBrandingTwiceWithoutCreatingResources()
    {
        var (client, handler) = CreateClient();
        var data = new GoogleWalletClassData("issuer.stable", "Program", "Tenant", null, null,
            "https://assets.test/hero.png", "#123456");
        await client.EnsureLoyaltyClassAsync(data);
        await client.EnsureLoyaltyClassAsync(data);
        Assert.Equal(2, handler.ApiRequests.Count(x => x.Method.Method == "PATCH"));
        Assert.DoesNotContain(handler.ApiRequests, x => x.Method == HttpMethod.Post);
        foreach (var request in handler.ApiRequests.Where(x => x.Method.Method == "PATCH"))
        {
            Assert.EndsWith("/loyaltyClass/issuer.stable", request.Uri, StringComparison.Ordinal);
            using var json = JsonDocument.Parse(request.Body);
            Assert.Equal("#123456", json.RootElement.GetProperty("hexBackgroundColor").GetString());
            Assert.Equal("https://assets.test/hero.png", json.RootElement.GetProperty("heroImage").GetProperty("sourceUri").GetProperty("uri").GetString());
        }
    }

    [Fact]
    [Trait("Category", "GoogleWalletNotifications")]
    public async Task AddMessage_uses_object_endpoint_and_TEXT_AND_NOTIFY()
    {
        using var rsa = RSA.Create(2048);
        var credentials = new GoogleWalletCredentials(
            "wallet@example.test",
            rsa.ExportPkcs8PrivateKeyPem(),
            "https://oauth.example.test/token");
        var credentialsProvider = new Mock<IGoogleWalletCredentialsProvider>();
        credentialsProvider.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(credentials);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc));
        var options = Options.Create(new GoogleWalletOptions
        {
            Enabled = true,
            IssuerId = "issuer-test",
            ApiBaseUrl = "https://walletobjects.example.test/walletobjects/v1"
        });
        var handler = new CaptureHandler();
        var client = new GoogleWalletClient(
            new HttpClient(handler),
            credentialsProvider.Object,
            new GoogleWalletJwtFactory(options),
            new GoogleWalletObjectMapper(),
            options,
            clock.Object,
            NullLogger<GoogleWalletClient>.Instance);

        await client.AddMessageAsync("issuer.object-a", "NOVEDAD", "A entrenar!", "notification-123");

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/loyaltyObject/issuer.object-a/addMessage", request.Uri, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(request.Body);
        var message = json.RootElement.GetProperty("message");
        Assert.Equal("NOVEDAD", message.GetProperty("header").GetString());
        Assert.Equal("A entrenar!", message.GetProperty("body").GetString());
        Assert.Equal("notification-123", message.GetProperty("id").GetString());
        Assert.Equal("TEXT_AND_NOTIFY", message.GetProperty("messageType").GetString());
    }

    [Fact]
    [Trait("Category", "GoogleWalletNotifications")]
    public async Task ExistingLoyaltyObject_RequestsNotificationOnlyWhenExplicitlyEnabled()
    {
        var (client, handler) = CreateClient();
        var data = ObjectData();

        await client.CreateOrUpdateObjectAsync(data, notifyOnUpdate: true);

        var patch = Assert.Single(handler.ApiRequests, x => x.Method.Method == "PATCH");
        using var json = JsonDocument.Parse(patch.Body);
        Assert.Equal("notifyOnUpdate", json.RootElement.GetProperty("notifyPreference").GetString());
        Assert.Equal(250, json.RootElement.GetProperty("loyaltyPoints").GetProperty("balance").GetProperty("int").GetInt32());
    }

    [Fact]
    public async Task ExistingLoyaltyObject_DoesNotRequestNotificationByDefault()
    {
        var (client, handler) = CreateClient();

        await client.CreateOrUpdateObjectAsync(ObjectData());

        var patch = Assert.Single(handler.ApiRequests, x => x.Method.Method == "PATCH");
        using var json = JsonDocument.Parse(patch.Body);
        Assert.False(json.RootElement.TryGetProperty("notifyPreference", out _));
    }

    [Fact]
    public async Task ExistingGiftCardClass_IsReadFromOfficialEndpoint()
    {
        var (client, handler) = CreateClient();
        await client.EnsureGiftCardClassAsync(new("issuer.giftcard_tenant", "Nuevo nombre"));
        var get = Assert.Single(handler.ApiRequests);
        Assert.Equal(HttpMethod.Get, get.Method);
        Assert.EndsWith("/giftCardClass/issuer.giftcard_tenant", get.Uri, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "GiftCards")]
    public async Task MissingGiftCardClass_CreatesOfficialGiftCardClassPayload()
    {
        var (client, handler) = CreateClient(giftCardResourcesExist: false);

        await client.EnsureGiftCardClassAsync(new("issuer.giftcard_tenant", "Nuevo nombre"));

        var post = Assert.Single(handler.ApiRequests, x => x.Method == HttpMethod.Post);
        Assert.EndsWith("/giftCardClass", post.Uri, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(post.Body);
        Assert.Equal("issuer.giftcard_tenant", json.RootElement.GetProperty("id").GetString());
        Assert.Equal("Nuevo nombre", json.RootElement.GetProperty("issuerName").GetString());
        Assert.Equal("UNDER_REVIEW", json.RootElement.GetProperty("reviewStatus").GetString());
        Assert.Equal(3, json.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task ExistingGiftCardObject_PatchesOfficialMoneyAndBarcodePayload()
    {
        var (client, handler) = CreateClient();
        await client.CreateOrUpdateGiftCardObjectAsync(new(
            "issuer.object_stable", "issuer.class_stable", "Regalos Tamalitos", "Ana", "Luis", "Disfrútala", "GC-AAAA-BBBB-CCCC",
            250m, "MXN", "Active", "#123456", "https://assets.test/logo.png", "https://assets.test/hero.png", null,
            new DateTime(2026, 9, 21, 12, 34, 56, DateTimeKind.Utc)));
        var patch = Assert.Single(handler.ApiRequests, x => x.Method.Method == "PATCH");
        Assert.EndsWith("/giftCardObject/issuer.object_stable", patch.Uri, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(patch.Body);
        Assert.Equal("issuer.object_stable", json.RootElement.GetProperty("id").GetString());
        Assert.Equal("issuer.class_stable", json.RootElement.GetProperty("classId").GetString());
        Assert.Equal("GC-AAAA-BBBB-CCCC", json.RootElement.GetProperty("cardNumber").GetString());
        Assert.Equal("250000000", json.RootElement.GetProperty("balance").GetProperty("micros").GetString());
        Assert.Equal("MXN", json.RootElement.GetProperty("balance").GetProperty("currencyCode").GetString());
        Assert.Equal("QR_CODE", json.RootElement.GetProperty("barcode").GetProperty("type").GetString());
        Assert.Equal("GC-AAAA-BBBB-CCCC", json.RootElement.GetProperty("barcode").GetProperty("value").GetString());
        Assert.Equal("0s", json.RootElement.GetProperty("balanceUpdateTime").GetProperty("utcOffset").GetString());
        Assert.False(json.RootElement.TryGetProperty("cardTitle", out _));
        Assert.False(json.RootElement.TryGetProperty("textModulesData", out _));
    }

    [Fact]
    [Trait("Category", "GiftCards")]
    public async Task MissingGiftCardObject_PostsToOfficialEndpointAndAcceptsCreateConflict()
    {
        var (client, handler) = CreateClient(giftCardResourcesExist: false);
        await client.CreateOrUpdateGiftCardObjectAsync(new(
            "issuer.object_stable", "issuer.class_stable", "Regalos", "Ana", null, null,
            "GC-AAAA-BBBB-CCCC", 1.25m, "mxn", "Active", "#123456", null, null, null,
            new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc)));

        var post = Assert.Single(handler.ApiRequests, x => x.Method == HttpMethod.Post);
        Assert.EndsWith("/giftCardObject", post.Uri, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(post.Body);
        Assert.Equal("1250000", json.RootElement.GetProperty("balance").GetProperty("micros").GetString());
        Assert.Equal("MXN", json.RootElement.GetProperty("balance").GetProperty("currencyCode").GetString());
    }

    private static (GoogleWalletClient Client, CaptureHandler Handler) CreateClient(bool giftCardResourcesExist = true)
    {
        using var rsa = RSA.Create(2048);
        var credentials = new GoogleWalletCredentials("wallet@example.test", rsa.ExportPkcs8PrivateKeyPem(), "https://oauth.example.test/token");
        var provider = new Mock<IGoogleWalletCredentialsProvider>(); provider.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(credentials);
        var clock = new Mock<IDateTimeProvider>(); clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));
        var options = Options.Create(new GoogleWalletOptions { Enabled = true, IssuerId = "issuer-test", ApiBaseUrl = "https://walletobjects.example.test/walletobjects/v1" });
        var handler = new CaptureHandler(giftCardResourcesExist);
        return (new GoogleWalletClient(new HttpClient(handler), provider.Object, new GoogleWalletJwtFactory(options), new GoogleWalletObjectMapper(), options, clock.Object, NullLogger<GoogleWalletClient>.Instance), handler);
    }

    private static GoogleWalletObjectData ObjectData() => new(
        "issuer.object-a", "issuer.class-a", "Alex", "SERIAL-1", 250, "Gold", "SERIAL-1", true,
        new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc), "250 puntos", "Gold", null, null, "SERIAL-1");
    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly bool _giftCardResourcesExist;

        public CaptureHandler(bool giftCardResourcesExist = true) => _giftCardResourcesExist = giftCardResourcesExist;

        public List<CapturedRequest> ApiRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.Host == "oauth.example.test")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"test-token\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
                };
            }

            ApiRequests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri.ToString(),
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct)));
            if (!_giftCardResourcesExist
                && request.Method == HttpMethod.Get
                && (request.RequestUri.AbsolutePath.Contains("/giftCardClass/", StringComparison.Ordinal)
                    || request.RequestUri.AbsolutePath.Contains("/giftCardObject/", StringComparison.Ordinal)))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Uri, string Body);
}
