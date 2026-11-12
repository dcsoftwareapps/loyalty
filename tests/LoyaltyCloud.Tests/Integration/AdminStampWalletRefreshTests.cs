extern alias AdminApp;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AdminApp::LoyaltyCloud.Admin.Auth;
using AdminApp::LoyaltyCloud.Admin.Services;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Common.Security;
using LoyaltyCloud.Domain.Enums;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class AdminStampWalletRefreshTests
{
    [Fact]
    public async Task Cashier_refresh_sends_signed_google_sync_to_api_after_apple_refresh()
    {
        const string secret = "test-admin-api-shared-secret-with-enough-length";
        const string tenantSlug = "qa-stamps";
        const string serial = "ST-123456";
        const string path = "/api/internal/stamps/google/ST-123456/sync";
        var tenantId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var apple = new Mock<IAppleWalletPassRefreshService>();
        apple.Setup(x => x.RefreshCardAsync(tenantId, cardId, PassUpdateReason.StampEarned,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleWalletPassRefreshResult(tenantId, [], 0, 0, 0, 0, 0, false,
                ApnPushFailureType.None, null));
        var transport = new SignedSyncTransport(secret, tenantSlug, path);
        var client = new HttpClient(transport) { BaseAddress = new Uri("https://api.test") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("LoyaltyCloudApi")).Returns(client);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(AdminClaimTypes.TenantSlug, tenantSlug), new Claim(AdminClaimTypes.Name, "qa-admin")], "test"));
        var auth = new Mock<AuthenticationStateProvider>();
        auth.Setup(x => x.GetAuthenticationStateAsync()).ReturnsAsync(new AuthenticationState(principal));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminApi:SharedSecret"] = secret,
            ["GoogleWallet:Enabled"] = "false"
        }).Build();
        var api = new AdminApiClient(factory.Object, auth.Object, config);
        var refresh = new AdminStampWalletRefreshService(apple.Object, api,
            NullLogger<AdminStampWalletRefreshService>.Instance);

        await refresh.RefreshCardAsync(tenantId, cardId, serial, PassUpdateReason.StampEarned);

        apple.Verify(x => x.RefreshCardAsync(tenantId, cardId, PassUpdateReason.StampEarned,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, transport.Calls);
    }

    private sealed class SignedSyncTransport(string secret, string tenantSlug, string path) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(path, request.RequestUri?.AbsolutePath);
            var timestamp = request.Headers.GetValues(AdminApiSignature.TimestampHeader).Single();
            var signature = request.Headers.GetValues(AdminApiSignature.SignatureHeader).Single();
            Assert.True(AdminApiSignature.VerifySignature(secret, "POST", path, timestamp, tenantSlug,
                "qa-admin", [], signature));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new GoogleStampWalletSyncResponse(true))
            });
        }
    }
}
