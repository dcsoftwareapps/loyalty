using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyCloud.Cashier.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category","CashierMobile")]
public sealed class CashierRefreshSessionTests
{
    private static CashierSession Session(bool expired=false)=>new("old-access","Bearer",DateTimeOffset.UtcNow.AddHours(expired ? -2 : 1),"tenant",Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),"user","Cashier","Stamps","refresh",DateTimeOffset.UtcNow.AddDays(90));
    private static HttpResponseMessage Success()=>new(HttpStatusCode.OK) { Content=JsonContent.Create(new CashierLoginResponse("new-access","Bearer",DateTimeOffset.UtcNow.AddHours(1),3600,"tenant",Session().UserId,"user","Cashier","Stamps","new-refresh",DateTimeOffset.UtcNow.AddDays(90))) };
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Startup_restores_valid_access_or_refreshes_expired_access(bool expired)
    {
        var store=new Store(); var calls=0; using var http=new HttpClient(new Transport(_=> { calls++; return Task.FromResult(Success()); })) {BaseAddress=new Uri("https://api.test/")};
        var api=new CashierApiClient(http); var old=new CashierSessionService(store,api); await old.SignInAsync(Session(expired));
        var restored=new CashierSessionService(store,api); var session=await restored.RestoreAsync(); Assert.NotNull(session); Assert.Equal(expired ? 1 : 0,calls);
        Assert.Equal(expired ? "new-access" : "old-access",session.AccessToken); Assert.True(old.Current!.IsSameSession(session));
        Assert.DoesNotContain("password",store.Raw!,StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Remember_false_has_no_restart_session_and_invalid_refresh_clears_persistent_auth()
    {
        var store=new Store(); using var http=new HttpClient(new Transport(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)))) {BaseAddress=new Uri("https://api.test/")};
        var api=new CashierApiClient(http); var service=new CashierSessionService(store,api);
        await service.SignInAsync(Session(),false); Assert.Null(service.Current!.RefreshToken); Assert.Null(store.Raw); Assert.Null(await new CashierSessionService(store,api).RestoreAsync());
        await service.SignInAsync(Session(true)); Assert.Null(await new CashierSessionService(store,api).RestoreAsync()); Assert.Null(store.Raw);
    }
    [Fact]
    public async Task Concurrent_refresh_is_single_flight_and_logout_never_restores()
    {
        var store=new Store(); var count=0; var sent=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http=new HttpClient(new Transport(async req=> { if (req.RequestUri!.AbsolutePath.EndsWith("logout")) return new HttpResponseMessage(HttpStatusCode.NoContent); Interlocked.Increment(ref count); sent.SetResult(); await release.Task; return Success(); })) {BaseAddress=new Uri("https://api.test/")};
        var api=new CashierApiClient(http); var service=new CashierSessionService(store,api); await service.SignInAsync(Session(true));
        var first=service.RefreshAfterUnauthorizedAsync("old-access"); await sent.Task; var second=service.RefreshAfterUnauthorizedAsync("old-access"); release.SetResult();
        Assert.All(await Task.WhenAll(first,second),Assert.True); Assert.Equal(1,count); Assert.Equal("new-refresh",service.Current!.RefreshToken);
        await service.LogoutAsync(); Assert.Null(service.Current); Assert.Null(store.Raw); Assert.Null(await new CashierSessionService(store,api).RestoreAsync());
    }
    [Fact]
    public async Task Transient_refresh_failure_retains_credentials_for_retry()
    {
        var store=new Store(); using var http=new HttpClient(new Transport(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))) {BaseAddress=new Uri("https://api.test/")};
        var api=new CashierApiClient(http); await new CashierSessionService(store,api).SignInAsync(Session(true));
        await Assert.ThrowsAsync<HttpRequestException>(()=>new CashierSessionService(store,api).RestoreAsync()); Assert.Contains("refresh",store.Raw!);
    }
    [Fact]
    public async Task Unauthorized_retries_once_with_identical_mutable_payload_and_key()
    {
        var store=new Store(); using var authHttp=new HttpClient(new Transport(_=>Task.FromResult(Success()))) {BaseAddress=new Uri("https://api.test/")};
        var service=new CashierSessionService(store,new CashierApiClient(authHttp)); await service.SignInAsync(Session());
        var bodies=new List<string>(); var tokens=new List<string?>();
        var handler=new CashierAuthorizationHandler(service,new CashierApiOptions("test",new Uri("https://api.test/"))) {
            InnerHandler=new Transport(async r=> { bodies.Add(await r.Content!.ReadAsStringAsync()); tokens.Add(r.Headers.Authorization?.Parameter); return new HttpResponseMessage(bodies.Count==1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK); }) };
        using var client=new HttpClient(handler) {BaseAddress=new Uri("https://api.test/")};
        using var response=await client.PostAsJsonAsync("api/stamps/KB-TEST/earn",new {idempotencyKey="original"}); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal(2,bodies.Count); Assert.Equal(bodies[0],bodies[1]); Assert.Contains("original",bodies[1]); Assert.Equal(new[]{"old-access","new-access"},tokens);
    }
    [Fact]
    public async Task Second_unauthorized_stops_retrying_and_clears_session()
    {
        var store=new Store(); var refreshes=0; var attempts=0;
        using var authHttp=new HttpClient(new Transport(_=> { refreshes++; return Task.FromResult(Success()); })) {BaseAddress=new Uri("https://api.test/")};
        var service=new CashierSessionService(store,new CashierApiClient(authHttp)); await service.SignInAsync(Session());
        using var client=new HttpClient(new CashierAuthorizationHandler(service,new CashierApiOptions("test",new Uri("https://api.test/"))) {
            InnerHandler=new Transport(_=> { attempts++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); }) }) {BaseAddress=new Uri("https://api.test/")};
        using var result=await client.GetAsync("api/customers/test");
        Assert.Equal(HttpStatusCode.Unauthorized,result.StatusCode); Assert.Equal(2,attempts); Assert.Equal(1,refreshes);
        Assert.Null(service.Current); Assert.Null(store.Raw);
    }
    [Fact]
    public async Task Offline_logout_clears_tokens_and_prevents_restart_restore()
    {
        var store=new Store(); using var http=new HttpClient(new Transport(_=>throw new HttpRequestException("offline"))) {BaseAddress=new Uri("https://api.test/")};
        var service=new CashierSessionService(store,new CashierApiClient(http)); await service.SignInAsync(Session());
        await service.LogoutAsync(); Assert.Null(service.Current); Assert.Null(store.Raw);
        Assert.Null(await new CashierSessionService(store,new CashierApiClient(http)).RestoreAsync());
    }
    [Fact]
    public async Task Token_rotation_keeps_session_identity_but_new_login_changes_it()
    {
        var store=new Store(); using var http=new HttpClient(new Transport(_=>Task.FromResult(Success()))) {BaseAddress=new Uri("https://api.test/")};
        var service=new CashierSessionService(store,new CashierApiClient(http)); await service.SignInAsync(Session()); var captured=service.Current!;
        Assert.True(await service.RefreshAfterUnauthorizedAsync(captured.AccessToken)); Assert.True(service.Current!.IsSameSession(captured));
        await service.SignInAsync(Session()); Assert.False(service.Current!.IsSameSession(captured));
    }
    [Theory]
    [InlineData("logout")] [InlineData("user")] [InlineData("tenant")] [InlineData("new-login")]
    public async Task Session_change_during_retry_buffering_never_resends_original_request(string change)
    {
        var refreshes = 0;
        using var authHttp = new HttpClient(new Transport(authRequest => {
            if (authRequest.RequestUri!.AbsolutePath.EndsWith("logout"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            refreshes++;
            return Task.FromResult(Success());
        })) { BaseAddress = new Uri("https://api.test/") };
        var service = new CashierSessionService(new Store(), new CashierApiClient(authHttp));
        await service.SignInAsync(Session());
        var tokens = new List<string?>();
        using var client = new HttpClient(new CashierAuthorizationHandler(service,
            new CashierApiOptions("test", new Uri("https://api.test/"))) {
            InnerHandler = new Transport(_request => {
                tokens.Add(_request.Headers.Authorization?.Parameter);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            })
        }) { BaseAddress = new Uri("https://api.test/") };
        // The initial fake transport does not read content. Buffering for retry is
        // the explicit interleaving point, after refresh has released its gate.
        using var content = new InterleavedContent(async () => {
            Assert.Equal("new-access", service.Current!.AccessToken);
            await service.LogoutAsync();
            if (change != "logout")
                await service.SignInAsync(Session() with {
                    AccessToken = "session-b-access",
                    UserId = change == "user" ? Guid.NewGuid() : Session().UserId,
                    TenantSlug = change == "tenant" ? "other-tenant" : "tenant"
                });
        });
        using var response = await client.PostAsync("api/stamps/KB-TEST/earn", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, refreshes);
        Assert.Equal(new[] { "old-access" }, tokens);
        if (change == "logout") Assert.Null(service.Current);
        else Assert.Equal("session-b-access", service.Current!.AccessToken);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)] [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Failed_refresh_does_not_resend_or_loop(HttpStatusCode status)
    {
        var refreshes = 0; var attempts = 0; var store = new Store();
        using var authHttp = new HttpClient(new Transport(_ => {
            refreshes++; return Task.FromResult(new HttpResponseMessage(status));
        })) { BaseAddress = new Uri("https://api.test/") };
        var service = new CashierSessionService(store, new CashierApiClient(authHttp));
        await service.SignInAsync(Session());
        using var client = new HttpClient(new CashierAuthorizationHandler(service,
            new CashierApiOptions("test", new Uri("https://api.test/"))) {
            InnerHandler = new Transport(_ => {
                attempts++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            })
        }) { BaseAddress = new Uri("https://api.test/") };
        if (status == HttpStatusCode.Unauthorized)
        {
            using var response = await client.PostAsJsonAsync("api/stamps/KB-TEST/earn", new { idempotencyKey = "original" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Null(service.Current);
        }
        else
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => client.PostAsJsonAsync("api/stamps/KB-TEST/earn", new { idempotencyKey = "original" }));
            Assert.NotNull(service.Current); Assert.NotNull(store.Raw);
        }
        Assert.Equal(1, refreshes); Assert.Equal(1, attempts);
    }

    private sealed class InterleavedContent(Func<Task> interleave) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            await interleave();
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("{\"idempotencyKey\":\"original\"}"));
        }
    }
    private sealed class Store : ISecureTokenStore { public string? Raw; public Task<string?> GetAsync(string key)=>Task.FromResult(Raw); public Task SetAsync(string key,string value) {Raw=value;return Task.CompletedTask;} public void Remove(string key)=>Raw=null; }
    private sealed class Transport(Func<HttpRequestMessage,Task<HttpResponseMessage>> send) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>send(r); }
}
