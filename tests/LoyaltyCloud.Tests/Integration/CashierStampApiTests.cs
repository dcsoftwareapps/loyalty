using System.Net;
using System.Net.Http.Json;
using LoyaltyCloud.Cashier.Services;
using LoyaltyCloud.Common.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampCashier")]
public sealed class CashierStampApiTests
{
    [Theory]
    [InlineData("Earn", "earn")]
    [InlineData("Reversal", "reverse-last")]
    [InlineData("Redeem", "rewards/11111111-1111-1111-1111-111111111111/redeem")]
    public async Task Uses_existing_endpoint_and_only_sends_idempotency_key(string operation, string suffix)
    {
        var handler = new Handler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"/api/stamps/KB-TEST/{suffix}", request.RequestUri!.AbsolutePath);
            using var body = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Single(body.RootElement.EnumerateObject());
            Assert.Equal("same-key", body.RootElement.GetProperty("idempotencyKey").GetString());
            return new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { detail = "No disponible." }) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new("https://example.test/") };
        var api = new CashierStampApi(new AuthenticatedCashierApiClient(http));
        var result = await api.ExecuteAsync(new(operation, "KB-TEST", "same-key", Guid.Parse("11111111-1111-1111-1111-111111111111")));
        Assert.False(result.Uncertain);
        Assert.Equal("No disponible.", result.Error);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(401)]
    public async Task Unknown_commit_outcome_is_never_treated_as_definitive(int status)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))))
            { BaseAddress = new("https://example.test/") };
        var api = new CashierStampApi(new AuthenticatedCashierApiClient(http));
        Assert.True((await api.ExecuteAsync(new("Earn", "KB-TEST", "key", null))).Uncertain);
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
