using System.Net;
using System.Security.Cryptography;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class ApnServiceTests
{
    [Fact]
    [Trait("Category", "APNs")]
    public async Task Http_200_returns_success()
    {
        var service = CreateService(HttpStatusCode.OK, "{}");

        var result = await service.SendPassUpdateAsync("push-token", PassUpdateReason.PointsAdded);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(ApnPushFailureType.None, result.FailureType);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [Trait("Category", "APNs")]
    public async Task Retryable_status_codes_return_transient_failure(int statusCode)
    {
        var service = CreateService((HttpStatusCode)statusCode, """{"reason":"TooManyRequests"}""");

        var result = await service.SendPassUpdateAsync("push-token", PassUpdateReason.PointsAdded);

        Assert.False(result.Success);
        Assert.Equal(statusCode, result.StatusCode);
        Assert.Equal(ApnPushFailureType.Transient, result.FailureType);
    }

    [Theory]
    [InlineData("BadDeviceToken")]
    [InlineData("Unregistered")]
    [InlineData("DeviceTokenNotForTopic")]
    [Trait("Category", "APNs")]
    public async Task Permanent_apns_reasons_do_not_return_success(string reason)
    {
        var service = CreateService(HttpStatusCode.BadRequest, $$"""{"reason":"{{reason}}"}""");

        var result = await service.SendPassUpdateAsync("push-token", PassUpdateReason.PointsAdded);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(reason, result.Reason);
        Assert.Equal(ApnPushFailureType.Permanent, result.FailureType);
    }

    private static ApnService CreateService(HttpStatusCode statusCode, string body)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        });
        var client = new HttpClient(handler);
        var options = Options.Create(new ApplePassOptions
        {
            ApnHost = "https://api.push.apple.com",
            PassTypeIdentifier = "pass.com.test"
        });

        return new ApnService(
            client,
            new TestAppleWalletSecretsProvider(),
            options,
            NullLogger<ApnService>.Instance);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responseFactory(request));
    }

    private sealed class TestAppleWalletSecretsProvider : IAppleWalletSecretsProvider
    {
        private readonly string _privateKeyPem;

        public TestAppleWalletSecretsProvider()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _privateKeyPem = key.ExportPkcs8PrivateKeyPem();
        }

        public Task<byte[]> GetPassCertificateBytesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<string> GetPassCertificatePasswordAsync(CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<byte[]?> GetWwdrCertificateBytesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<byte[]?>(null);

        public Task<string> GetApnPrivateKeyPemAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_privateKeyPem);

        public Task<string> GetApnKeyIdAsync(CancellationToken cancellationToken) =>
            Task.FromResult("KEYID12345");

        public Task<string> GetApnTeamIdAsync(CancellationToken cancellationToken) =>
            Task.FromResult("TEAMID1234");
    }
}
