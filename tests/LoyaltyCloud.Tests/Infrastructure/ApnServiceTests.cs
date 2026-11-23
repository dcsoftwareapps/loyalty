using System.Net;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    [Trait("Category", "APNs")]
    public async Task Separate_clients_reuse_the_same_signed_token_and_preserve_http_request()
    {
        using var cache = new ApnProviderTokenCache();
        var secrets = new TestAppleWalletSecretsProvider();
        var tokens = new List<string>();
        var issuedAfter = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var handler = new StubHttpMessageHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.push.apple.com/3/device/push-token", request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpVersion.Version20, request.Version);
            Assert.Equal(HttpVersionPolicy.RequestVersionExact, request.VersionPolicy);
            Assert.Equal("bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("pass.com.test", Assert.Single(request.Headers.GetValues("apns-topic")));
            Assert.Equal("background", Assert.Single(request.Headers.GetValues("apns-push-type")));
            Assert.Equal("5", Assert.Single(request.Headers.GetValues("apns-priority")));
            Assert.Equal("{}", await request.Content!.ReadAsStringAsync());
            Assert.Equal("application/json", request.Content.Headers.ContentType!.MediaType);
            tokens.Add(request.Headers.Authorization.Parameter!);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = new HttpClient(handler);
        var options = Options.Create(new ApplePassOptions { ApnHost = "https://api.push.apple.com", PassTypeIdentifier = "pass.com.test" });
        var first = new ApnService(http, secrets, options, NullLogger<ApnService>.Instance, cache);
        var second = new ApnService(http, secrets, options, NullLogger<ApnService>.Instance, cache);
        Assert.True((await first.SendPassUpdateAsync("push-token", PassUpdateReason.PointsAdded)).Success);
        Assert.True((await second.SendPassUpdateAsync("push-token", PassUpdateReason.PointsAdded)).Success);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(tokens[0], tokens[1]);
        Assert.Equal(1, secrets.PrivateKeyReads);
        var parts = tokens[0].Split('.');
        Assert.Equal(3, parts.Length);
        using var header = JsonDocument.Parse(Decode(parts[0]));
        using var claims = JsonDocument.Parse(Decode(parts[1]));
        Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("KEYID12345", header.RootElement.GetProperty("kid").GetString());
        Assert.Equal("JWT", header.RootElement.GetProperty("typ").GetString());
        Assert.Equal("TEAMID1234", claims.RootElement.GetProperty("iss").GetString());
        Assert.InRange(claims.RootElement.GetProperty("iat").GetInt64(), issuedAfter, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(secrets.PublicKey, out _);
        Assert.True(verifier.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact]
    [Trait("Category", "APNs")]
    public async Task Concurrent_clients_read_signing_credentials_once()
    {
        using var cache = new ApnProviderTokenCache();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secrets = new TestAppleWalletSecretsProvider { PrivateKeyGate = release.Task };
        var tokens = new ConcurrentBag<string>();
        var requests = Enumerable.Range(0, 16).Select(_ => CreateService(HttpStatusCode.OK, "{}", cache, secrets,
            request => tokens.Add(request.Headers.Authorization!.Parameter!))
            .SendPassUpdateAsync("push-token", PassUpdateReason.PointsAdded)).ToArray();
        Assert.Equal(1, secrets.PrivateKeyReads);
        release.SetResult();
        var results = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(results, result => Assert.True(result.Success));
        Assert.Equal(16, tokens.Count);
        Assert.Single(tokens.Distinct());
        Assert.Equal(1, secrets.PrivateKeyReads);
    }

    [Fact]
    [Trait("Category", "APNs")]
    public async Task Too_many_provider_token_updates_does_not_invalidate_token_or_retry()
    {
        using var cache = new ApnProviderTokenCache();
        var secrets = new TestAppleWalletSecretsProvider();
        var tokens = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            var service = CreateService((HttpStatusCode)429, """{"reason":"TooManyProviderTokenUpdates"}""", cache, secrets,
                request => tokens.Add(request.Headers.Authorization!.Parameter!));
            var result = await service.SendPassUpdateAsync("push-token", PassUpdateReason.PointsAdded);
            Assert.Equal(ApnPushFailureType.Transient, result.FailureType);
            Assert.Equal(429, result.StatusCode);
            Assert.Equal("TooManyProviderTokenUpdates", result.Reason);
        }
        Assert.Equal(2, tokens.Count); // One HTTP request per explicit send, without an automatic retry.
        Assert.Equal(tokens[0], tokens[1]);
        Assert.Equal(1, secrets.PrivateKeyReads);
    }

    [Fact]
    [Trait("Category", "APNs")]
    public async Task Production_di_shares_cache_between_scopes_without_capturing_scoped_secrets()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<ApplePassOptions>(o => { o.ApnHost = "https://api.push.apple.com"; o.PassTypeIdentifier = "pass.com.test"; });
        var created = new List<TestAppleWalletSecretsProvider>();
        services.AddScoped<IAppleWalletSecretsProvider>(_ =>
        {
            var secrets = new TestAppleWalletSecretsProvider(); created.Add(secrets); return secrets;
        });
        // Exercise the actual production registration without configuring Azure, files or a database.
        typeof(LoyaltyCloud.Infrastructure.DependencyInjection).GetMethod("AddApnHttpClient", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [services]);
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() =>
            new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));
        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services.Where(x => x.ServiceType == typeof(ApnProviderTokenCache))).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services.Where(x => x.ServiceType == typeof(IAppleWalletSecretsProvider))).Lifetime);
        Assert.Equal(ServiceLifetime.Transient, Assert.Single(services.Where(x => x.ServiceType == typeof(IApnService))).Lifetime);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var cache = provider.GetRequiredService<ApnProviderTokenCache>();
        IApnService first;
        using (var scope = provider.CreateScope())
        {
            Assert.Same(cache, scope.ServiceProvider.GetRequiredService<ApnProviderTokenCache>());
            first = scope.ServiceProvider.GetRequiredService<IApnService>();
            Assert.True((await first.SendPassUpdateAsync("push-token", PassUpdateReason.PointsAdded)).Success);
        }
        Assert.True(created[0].Disposed);
        using (var scope = provider.CreateScope())
        {
            Assert.Same(cache, scope.ServiceProvider.GetRequiredService<ApnProviderTokenCache>());
            var second = scope.ServiceProvider.GetRequiredService<IApnService>();
            Assert.NotSame(first, second);
            Assert.True((await second.SendPassUpdateAsync("push-token", PassUpdateReason.PointsAdded)).Success);
            Assert.Equal(0, created[1].PrivateKeyReads);
        }
        Assert.Equal(2, created.Count);
        Assert.All(created, secrets => Assert.True(secrets.Disposed));
        Assert.Equal(1, created.Sum(secrets => secrets.PrivateKeyReads));
    }

    private static byte[] Decode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
    }

    private static ApnService CreateService(HttpStatusCode statusCode, string body,
        ApnProviderTokenCache? cache = null, TestAppleWalletSecretsProvider? secrets = null,
        Action<HttpRequestMessage>? observe = null)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            observe?.Invoke(request);
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(body) });
        });
        var client = new HttpClient(handler);
        var options = Options.Create(new ApplePassOptions
        {
            ApnHost = "https://api.push.apple.com",
            PassTypeIdentifier = "pass.com.test"
        });

        return new ApnService(
            client,
            secrets ?? new TestAppleWalletSecretsProvider(),
            options,
            NullLogger<ApnService>.Instance,
            cache ?? new ApnProviderTokenCache());
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _responseFactory(request);
    }

    private sealed class TestAppleWalletSecretsProvider : IAppleWalletSecretsProvider, IDisposable
    {
        private readonly string _privateKeyPem;
        private int _privateKeyReads;
        public int PrivateKeyReads => Volatile.Read(ref _privateKeyReads);
        public byte[] PublicKey { get; }
        public Task? PrivateKeyGate { get; init; }
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;

        public TestAppleWalletSecretsProvider()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _privateKeyPem = key.ExportPkcs8PrivateKeyPem();
            PublicKey = key.ExportSubjectPublicKeyInfo();
        }

        public Task<byte[]> GetPassCertificateBytesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<string> GetPassCertificatePasswordAsync(CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<byte[]?> GetWwdrCertificateBytesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<byte[]?>(null);

        public async Task<string> GetApnPrivateKeyPemAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            Interlocked.Increment(ref _privateKeyReads);
            if (PrivateKeyGate is not null) await PrivateKeyGate.WaitAsync(cancellationToken);
            return _privateKeyPem;
        }

        public Task<string> GetApnKeyIdAsync(CancellationToken cancellationToken) =>
            Task.FromResult("KEYID12345");

        public Task<string> GetApnTeamIdAsync(CancellationToken cancellationToken) =>
            Task.FromResult("TEAMID1234");
    }
}
