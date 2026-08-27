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

    private sealed class CaptureHandler : HttpMessageHandler
    {
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
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Uri, string Body);
}
