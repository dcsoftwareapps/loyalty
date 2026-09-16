using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyCloud.Cashier.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "CashierMobile")]
public sealed class CashierGiftCardMobileTests
{
    private const string Code = "GC-AAAA-BBBB-CCCC";
    private static CashierSession Session(string tenant = "one", Guid? user = null) => new("test-token", "Bearer",
        DateTimeOffset.UtcNow.AddHours(1), tenant, user ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "cashier", "Cashier");
    private static CashierGiftCard Card(string code = Code) => new(code, 100m, 75m, "MXN", "Active", null, true, "Cliente", "Daniel", "Feliz cumple");
    private static GiftCardResult<GiftCardReceipt> Receipt() => new(new(25m, Card(), false));
    private static GiftCardIssueOptions IssueOptions() => new("MXN", true, "Never", null,
        [new(100m, "MXN"), new(200m, "MXN")]);
    private static GiftCardIssueReceipt IssueReceipt() => new(new("GC-ZZZZ-YYYY-XXXX", 200m, 200m, "MXN", "Active", null, true, "Cliente", "Daniel", "Feliz cumple"), "https://admin.example.test/giftcards/claim/token");

    [Theory]
    [InlineData(" gc-aaaa-bbbb-cccc ", Code, null)]
    [InlineData("https://admin.loyaltycloud.net/giftcards/claim/AbC123", null, "AbC123")]
    [InlineData("https://other-host.test/giftcards/claim/AbC123", null, "AbC123")]
    [InlineData("https://admin.loyaltycloud.net/giftcards/claim/%41bc", null, "Abc")]
    public void Parser_extracts_only_an_identifier(string raw, string? code, string? token)
    {
        Assert.Equal(new GiftCardLookup(code, token), GiftCardQrParser.Parse(raw));
        Assert.Equal(raw.Trim(), CashierQrPayloadParser.ExtractCustomerSerial(raw));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("KB-CUSTOMER")]
    [InlineData("text GC-AAAA-BBBB-CCCC text")]
    [InlineData("http://example.test/giftcards/claim/token")]
    [InlineData("https://user:pass@example.test/giftcards/claim/token")]
    [InlineData("https://example.test/giftcards/claim/token/extra")]
    [InlineData("https://example.test/giftcards/claim/token?redirect=evil")]
    [InlineData("https://example.test/giftcards/claim/%2Ftoken")]
    [InlineData("https://example.test/giftcards/claim/token#fragment")]
    public void Parser_rejects_unsupported_payloads(string? value) => Assert.Null(GiftCardQrParser.Parse(value));

    [Fact]
    public async Task Http_client_uses_only_authenticated_api_routes_and_exact_request_contracts()
    {
        var handler = new RecordingHandler();
        handler.Reply = request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(request.RequestUri!.AbsolutePath.EndsWith("lookup") ? (object)Card() :
            request.RequestUri!.AbsolutePath.EndsWith("issue/options") ? IssueOptions() :
            request.RequestUri!.AbsolutePath.EndsWith("issue") ? IssueReceipt() : new GiftCardReceipt(25m, Card(), false)) });
        var api = Api(handler);
        Assert.True((await api.GetIssueOptionsAsync()).Succeeded);
        Assert.True((await api.IssueAsync(new(200m, "Cliente", null, "Daniel", "Feliz cumple", null, "issue-key"))).Succeeded);
        Assert.True((await api.LookupAsync(new(null, "secret-claim"))).Succeeded);
        Assert.True((await api.RedeemAsync(Code, new(25m, "same-key"))).Succeeded);
        Assert.Equal("https://api.example.test/api/giftcards/issue/options", handler.Requests[0].Url);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("https://api.example.test/api/giftcards/issue", handler.Requests[1].Url);
        Assert.Equal("https://api.example.test/api/giftcards/lookup", handler.Requests[2].Url);
        Assert.Equal("https://api.example.test/api/giftcards/" + Code + "/redeem", handler.Requests[3].Url);
        Assert.All(handler.Requests.Skip(1), r => Assert.Equal(HttpMethod.Post, r.Method));
        using var issueJson = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal(200m, issueJson.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("Cliente", issueJson.RootElement.GetProperty("recipientName").GetString());
        Assert.Equal("Daniel", issueJson.RootElement.GetProperty("senderName").GetString());
        Assert.Equal("Feliz cumple", issueJson.RootElement.GetProperty("personalMessage").GetString());
        Assert.Equal("issue-key", issueJson.RootElement.GetProperty("idempotencyKey").GetString());
        using var json = JsonDocument.Parse(handler.Requests[3].Body);
        Assert.Equal(25m, json.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("same-key", json.RootElement.GetProperty("idempotencyKey").GetString());
        Assert.DoesNotContain("tenant", handler.Requests[1].Body + handler.Requests[3].Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operator", handler.Requests[1].Body + handler.Requests[3].Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-claim", handler.Requests[2].Url);
    }

    [Theory]
    [InlineData(401, "", "Unauthorized", false)]
    [InlineData(403, "Unavailable", "Unavailable", false)]
    [InlineData(404, "NotFound", "NotFound", true)]
    [InlineData(409, "IdempotencyConflict", "IdempotencyConflict", false)]
    [InlineData(409, "ConcurrencyConflict", "ConcurrencyConflict", true)]
    [InlineData(422, "InsufficientBalance", "InsufficientBalance", true)]
    [InlineData(422, "Inactive", "Inactive", true)]
    [InlineData(422, "Expired", "Expired", true)]
    [InlineData(422, "PartialRedemptionNotAllowed", "PartialRedemptionNotAllowed", true)]
    [InlineData(400, "InvalidInput", "InvalidInput", true)]
    [InlineData(502, "", "Uncertain", false)]
    public async Task Http_errors_are_distinct_and_do_not_echo_server_secrets(int status, string code, string expected, bool definitive)
    {
        var handler = new RecordingHandler { Reply = _ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = JsonContent.Create(new { code, detail = "do-not-display-secret" }) }) };
        var result = await Api(handler).RedeemAsync(Code, new(25m, "key"));
        Assert.Equal(expected, result.Error);
        Assert.Equal(definitive, result.DefinitiveRejection);
        Assert.DoesNotContain("do-not-display-secret", result.Message!);
    }

    [Theory]
    [InlineData("{}")] [InlineData("not-json")] [InlineData("null")]
    public async Task Malformed_success_is_uncertain(string body)
    {
        var handler = new RecordingHandler { Reply = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }) };
        Assert.Equal("Uncertain", (await Api(handler).RedeemAsync(Code, new(25m, "key"))).Error);
    }

    [Fact]
    public async Task Timeout_then_restart_reuses_identical_payload_and_clears_only_after_confirmation()
    {
        var store = new MemoryStore(); var session = Session(); var handler = new RecordingHandler();
        handler.Reply = _ => throw new TaskCanceledException();
        var api = Api(handler);
        var first = Coordinator(api, store, () => session);
        Assert.False((await first.BeginAsync(Code, 25m)).Succeeded);
        Assert.NotNull(await first.LoadAsync());
        Assert.DoesNotContain("test-token", string.Join("", store.Data.Values));
        var restarted = Coordinator(api, store, () => session);
        Assert.Equal("Pending", (await restarted.BeginAsync("GC-DDDD-EEEE-FFFF", 10m)).Error);
        handler.Reply = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new GiftCardReceipt(25m, Card(), true)) });
        Assert.True((await restarted.RetryAsync()).Succeeded);
        Assert.Equal(handler.Requests[0].Body, handler.Requests[1].Body);
        Assert.Equal(handler.Requests[0].Url, handler.Requests[1].Url);
        Assert.Null(await restarted.LoadAsync());
    }

    [Fact]
    public async Task Logout_other_user_and_other_tenant_cannot_replay_pending_operation()
    {
        var store = new MemoryStore(); var original = Session(); CashierSession? active = original;
        var api = new FakeApi { Reply = () => Task.FromResult(GiftCardResult<GiftCardReceipt>.Fail("Uncertain", "timeout")) };
        var coordinator = Coordinator(api, store, () => active);
        await coordinator.BeginAsync(Code, 25m);
        active = null;
        Assert.Equal("Unauthorized", (await coordinator.RetryAsync()).Error);
        active = Session(user: Guid.NewGuid());
        Assert.Null(await coordinator.LoadAsync());
        Assert.Equal("NoPending", (await coordinator.RetryAsync()).Error);
        active = Session("two");
        Assert.Null(await coordinator.LoadAsync());
        Assert.Equal("NoPending", (await coordinator.RetryAsync()).Error);
        Assert.Equal(1, api.Calls);
        active = original with { AccessToken = "renewed" };
        api.Reply = () => Task.FromResult(Receipt());
        Assert.True((await coordinator.RetryAsync()).Succeeded);
        Assert.Equal(2, api.Calls);
    }

    [Fact]
    public async Task Concurrent_submissions_do_not_send_twice()
    {
        var tcs = new TaskCompletionSource<GiftCardResult<GiftCardReceipt>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new FakeApi { Reply = () => tcs.Task }; var session = Session();
        var coordinator = Coordinator(api, new MemoryStore(), () => session);
        var first = coordinator.BeginAsync(Code, 25m);
        Assert.Equal("Busy", (await coordinator.BeginAsync(Code, 25m)).Error);
        Assert.Equal("Busy", (await coordinator.RetryAsync()).Error);
        tcs.SetResult(Receipt());
        Assert.True((await first).Succeeded);
        Assert.Equal(1, api.Calls);
    }

    [Fact]
    public async Task Failed_storage_prevents_http_and_corrupt_storage_blocks_new_operations()
    {
        var store = new MemoryStore { FailWrites = true }; var api = new FakeApi(); var session = Session();
        var coordinator = Coordinator(api, store, () => session);
        Assert.False((await coordinator.BeginAsync(Code, 25m)).Succeeded);
        Assert.Equal(0, api.Calls);
        store.FailWrites = false;
        api.Reply = () => Task.FromResult(GiftCardResult<GiftCardReceipt>.Fail("Uncertain", "timeout"));
        await coordinator.BeginAsync(Code, 25m);
        store.Data[store.Data.Keys.Single()] = "broken-json";
        Assert.False((await coordinator.BeginAsync(Code, 25m)).Succeeded);
        Assert.Equal(1, api.Calls);
    }

    [Fact]
    public async Task Rejection_after_uncertainty_does_not_allow_new_operation()
    {
        var session = Session(); var api = new FakeApi { Reply = () => Task.FromResult(GiftCardResult<GiftCardReceipt>.Fail("Uncertain", "timeout")) };
        var coordinator = Coordinator(api, new MemoryStore(), () => session);
        await coordinator.BeginAsync(Code, 25m);
        api.Reply = () => Task.FromResult(GiftCardResult<GiftCardReceipt>.Fail("InsufficientBalance", "insufficient", true));
        Assert.False((await coordinator.RetryAsync()).Succeeded);
        Assert.NotNull(await coordinator.LoadAsync());
        Assert.Equal("Pending", (await coordinator.BeginAsync(Code, 10m)).Error);
    }

    [Fact]
    public async Task Direct_definitive_rejection_allows_corrected_operation()
    {
        var session = Session(); var api = new FakeApi { Reply = () => Task.FromResult(GiftCardResult<GiftCardReceipt>.Fail("InsufficientBalance", "insufficient", true)) };
        var coordinator = Coordinator(api, new MemoryStore(), () => session);
        await coordinator.BeginAsync(Code, 25m);
        Assert.Null(await coordinator.LoadAsync());
    }

    [Fact]
    public async Task Mismatched_confirmation_does_not_clear_pending_or_report_success()
    {
        var session = Session();
        var api = new FakeApi { Reply = () => Task.FromResult(new GiftCardResult<GiftCardReceipt>(new(10m, Card(), false))) };
        var coordinator = Coordinator(api, new MemoryStore(), () => session);
        Assert.Equal("Uncertain", (await coordinator.BeginAsync(Code, 25m)).Error);
        Assert.NotNull(await coordinator.LoadAsync());
    }

    [Fact]
    public async Task Failure_to_clear_after_success_keeps_same_operation_for_safe_replay()
    {
        var session = Session(); var store = new MemoryStore();
        var api = new FakeApi { Reply = () => { store.FailWrites = true; return Task.FromResult(Receipt()); } };
        var coordinator = Coordinator(api, store, () => session);
        Assert.False((await coordinator.BeginAsync(Code, 25m)).Succeeded);
        Assert.NotNull(await coordinator.LoadAsync());
        store.FailWrites = false;
        api.Reply = () => Task.FromResult(Receipt());
        Assert.True((await coordinator.RetryAsync()).Succeeded);
        Assert.Null(await coordinator.LoadAsync());
    }

    [Fact]
    public async Task Pending_is_isolated_by_api_environment()
    {
        var session = Session(); var store = new MemoryStore();
        var api = new FakeApi { Reply = () => Task.FromResult(GiftCardResult<GiftCardReceipt>.Fail("Uncertain", "timeout")) };
        var staging = Coordinator(api, store, () => session);
        await staging.BeginAsync(Code, 25m);
        var production = new GiftCardRedemptionCoordinator(api, store, () => session, "https://production.example.test");
        Assert.Null(await production.LoadAsync());
        Assert.Equal("NoPending", (await production.RetryAsync()).Error);
        Assert.NotNull(await staging.LoadAsync());
        Assert.Equal(1, api.Calls);
    }

    [Fact]
    public async Task Issuance_timeout_then_restart_reuses_identical_payload_and_clears_after_confirmation()
    {
        var store = new IssueMemoryStore(); var session = Session(); var api = new FakeApi();
        api.IssueReply = () => Task.FromResult(GiftCardResult<GiftCardIssueReceipt>.Fail("Uncertain", "timeout"));
        var first = IssueCoordinator(api, store, () => session);
        Assert.False((await first.BeginAsync(new(200m, "Cliente", null, "Daniel", "Feliz cumple"))).Succeeded);
        Assert.NotNull(await first.LoadAsync());
        var saved = Assert.Single(store.Data.Values);
        Assert.DoesNotContain("test-token", saved);
        using (var savedJson = JsonDocument.Parse(saved))
        {
            var request = savedJson.RootElement.GetProperty("request");
            Assert.Equal(200m, request.GetProperty("amount").GetDecimal());
            Assert.Equal("Cliente", request.GetProperty("recipientName").GetString());
            Assert.Equal("Daniel", request.GetProperty("senderName").GetString());
            Assert.Equal("Feliz cumple", request.GetProperty("personalMessage").GetString());
            Assert.False(string.IsNullOrWhiteSpace(request.GetProperty("idempotencyKey").GetString()));
        }

        var restarted = IssueCoordinator(api, store, () => session);
        Assert.Equal("Pending", (await restarted.BeginAsync(new(100m, "Otro"))).Error);
        api.IssueReply = () => Task.FromResult(new GiftCardResult<GiftCardIssueReceipt>(IssueReceipt()));
        Assert.True((await restarted.RetryAsync()).Succeeded);
        Assert.Null(await restarted.LoadAsync());
        Assert.Equal(2, api.IssueCalls);
    }

    [Fact]
    public async Task Issuance_failure_to_clear_after_success_keeps_same_operation_for_safe_replay()
    {
        var session = Session(); var store = new IssueMemoryStore();
        var api = new FakeApi { IssueReply = () => { store.FailWrites = true; return Task.FromResult(new GiftCardResult<GiftCardIssueReceipt>(IssueReceipt())); } };
        var coordinator = IssueCoordinator(api, store, () => session);
        Assert.False((await coordinator.BeginAsync(new(200m, "Cliente"))).Succeeded);
        Assert.NotNull(await coordinator.LoadAsync());
        store.FailWrites = false;
        api.IssueReply = () => Task.FromResult(new GiftCardResult<GiftCardIssueReceipt>(IssueReceipt()));
        Assert.True((await coordinator.RetryAsync()).Succeeded);
        Assert.Null(await coordinator.LoadAsync());
    }

    [Fact]
    public async Task Issuance_pending_is_isolated_by_api_environment_and_account()
    {
        var session = Session(); var store = new IssueMemoryStore();
        var api = new FakeApi { IssueReply = () => Task.FromResult(GiftCardResult<GiftCardIssueReceipt>.Fail("Uncertain", "timeout")) };
        var staging = IssueCoordinator(api, store, () => session);
        await staging.BeginAsync(new(200m, "Cliente"));
        var production = new GiftCardIssuanceCoordinator(api, store, () => session, "https://production.example.test");
        Assert.Null(await production.LoadAsync());
        Assert.Equal("NoPending", (await production.RetryAsync()).Error);
        Assert.NotNull(await staging.LoadAsync());
        Assert.Equal(1, api.IssueCalls);
    }

    [Fact]
    public async Task Session_change_during_persistence_prevents_sending_under_another_account()
    {
        CashierSession? active = Session(); var original = active; var store = new MemoryStore(); var api = new FakeApi();
        store.AfterWrite = () => active = Session("other");
        var coordinator = Coordinator(api, store, () => active);
        Assert.Equal("Unauthorized", (await coordinator.BeginAsync(Code, 25m)).Error);
        Assert.Equal(0, api.Calls);
        active = original;
        Assert.NotNull(await coordinator.LoadAsync());
    }

    private static GiftCardRedemptionCoordinator Coordinator(ICashierGiftCardApi api, MemoryStore store, Func<CashierSession?> session) => new(api, store, session, "https://api.example.test");
    private static GiftCardIssuanceCoordinator IssueCoordinator(ICashierGiftCardApi api, IssueMemoryStore store, Func<CashierSession?> session) => new(api, store, session, "https://api.example.test");
    private static CashierGiftCardApi Api(RecordingHandler handler) => new(new AuthenticatedCashierApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") }));
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Url, HttpMethod Method, string Body)> Requests { get; } = [];
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Reply { get; set; } = _ => throw new NotImplementedException();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.AbsoluteUri, request.Method, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct)));
            return await Reply(request);
        }
    }
    private sealed class MemoryStore : IGiftCardPendingStore
    {
        public Dictionary<string, string> Data { get; } = [];
        public bool FailWrites { get; set; }
        public Action? AfterWrite { get; set; }
        public Task<string?> GetAsync(string key) => Task.FromResult(Data.GetValueOrDefault(key));
        public Task SetAsync(string key, string value)
        {
            if (FailWrites) throw new IOException();
            Data[key] = value; AfterWrite?.Invoke(); return Task.CompletedTask;
        }
    }
    private sealed class IssueMemoryStore : IGiftCardIssuancePendingStore
    {
        public Dictionary<string, string> Data { get; } = [];
        public bool FailWrites { get; set; }
        public Task<string?> GetAsync(string key) => Task.FromResult(Data.GetValueOrDefault(key));
        public Task SetAsync(string key, string value)
        {
            if (FailWrites) throw new IOException();
            Data[key] = value; return Task.CompletedTask;
        }
    }
    private sealed class FakeApi : ICashierGiftCardApi
    {
        public int Calls { get; private set; }
        public int IssueCalls { get; private set; }
        public Func<Task<GiftCardResult<GiftCardReceipt>>> Reply { get; set; } = () => Task.FromResult(Receipt());
        public Func<Task<GiftCardResult<GiftCardIssueReceipt>>> IssueReply { get; set; } = () => Task.FromResult(new GiftCardResult<GiftCardIssueReceipt>(IssueReceipt()));
        public Task<GiftCardResult<GiftCardIssueOptions>> GetIssueOptionsAsync() => Task.FromResult(new GiftCardResult<GiftCardIssueOptions>(IssueOptions()));
        public Task<GiftCardResult<GiftCardIssueReceipt>> IssueAsync(GiftCardIssueRequest request) { IssueCalls++; return IssueReply(); }
        public Task<GiftCardResult<CashierGiftCard>> LookupAsync(GiftCardLookup lookup) => Task.FromResult(new GiftCardResult<CashierGiftCard>(Card()));
        public Task<GiftCardResult<GiftCardReceipt>> RedeemAsync(string code, GiftCardRedemption request) { Calls++; return Reply(); }
    }
}
