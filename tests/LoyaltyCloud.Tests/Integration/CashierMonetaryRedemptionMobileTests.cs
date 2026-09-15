using LoyaltyCloud.Cashier.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "CashierMobile")]
[Trait("Category", "MonetaryRedemption")]
public sealed class CashierMonetaryRedemptionMobileTests
{
    private static CashierSession Session(string tenant = "one", Guid? user = null) => new(
        "test-token",
        "Bearer",
        DateTimeOffset.UtcNow.AddHours(1),
        tenant,
        user ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        "cashier",
        "Cashier");

    private static CashierMonetaryRedemptionPreview Preview() => new(
        "KB-TEST",
        100,
        10m,
        "MXN",
        10m,
        250,
        150);

    private static CashierRedemptionResponse Response(Guid? id = null) => new(
        id ?? Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
        "Descuento en dinero",
        100,
        150,
        0,
        DateTime.UtcNow,
        10m,
        "MXN",
        10m);

    [Fact]
    public async Task Timeout_then_retry_reuses_same_idempotency_key_and_monetary_snapshot()
    {
        var store = new MemoryStore();
        var session = Session();
        var api = new FakeApi
        {
            Reply = () => Task.FromResult(
                CashierOperationResult<CashierRedemptionResponse>.UncertainFailure("timeout"))
        };
        var coordinator = Coordinator(api, store, () => session);

        var first = await coordinator.BeginAsync(Preview());

        Assert.False(first.Succeeded);
        Assert.True(first.Uncertain);
        Assert.NotNull(await coordinator.LoadAsync());
        Assert.DoesNotContain("test-token", string.Join("", store.Data.Values));

        api.Reply = () => Task.FromResult(CashierOperationResult<CashierRedemptionResponse>.Success(Response()));
        var retry = await coordinator.RetryAsync();

        Assert.True(retry.Succeeded);
        Assert.Equal(2, api.Calls.Count);
        Assert.Equal(api.Calls[0].IdempotencyKey, api.Calls[1].IdempotencyKey);
        Assert.Equal(api.Calls[0].Preview.SerialNumber, api.Calls[1].Preview.SerialNumber);
        Assert.Equal(api.Calls[0].Preview.PointsToRedeem, api.Calls[1].Preview.PointsToRedeem);
        Assert.Equal(api.Calls[0].Preview.MonetaryAmount, api.Calls[1].Preview.MonetaryAmount);
        Assert.Equal(api.Calls[0].Preview.MonetaryPointsPerPesoUnit, api.Calls[1].Preview.MonetaryPointsPerPesoUnit);
        Assert.NotNull((await coordinator.LoadAsync())?.CreatedRedemption);
    }

    [Fact]
    public async Task Pending_operation_is_isolated_by_api_tenant_and_user()
    {
        var store = new MemoryStore();
        var original = Session();
        CashierSession? active = original;
        var api = new FakeApi
        {
            Reply = () => Task.FromResult(
                CashierOperationResult<CashierRedemptionResponse>.UncertainFailure("timeout"))
        };
        var staging = Coordinator(api, store, () => active);

        await staging.BeginAsync(Preview());

        active = Session(user: Guid.NewGuid());
        Assert.Null(await staging.LoadAsync());
        Assert.Equal("No hay un descuento pendiente.", (await staging.RetryAsync()).ErrorMessage);

        active = Session("other");
        Assert.Null(await staging.LoadAsync());

        active = original;
        var production = new MonetaryRedemptionCoordinator(api, store, () => active, "https://production.example.test");
        Assert.Null(await production.LoadAsync());
        Assert.Equal("No hay un descuento pendiente.", (await production.RetryAsync()).ErrorMessage);

        Assert.NotNull(await staging.LoadAsync());
        Assert.Single(api.Calls);
    }

    [Fact]
    public async Task Direct_definitive_rejection_clears_pending_but_uncertain_retry_does_not()
    {
        var store = new MemoryStore();
        var session = Session();
        var api = new FakeApi
        {
            Reply = () => Task.FromResult(
                CashierOperationResult<CashierRedemptionResponse>.Failure("Saldo insuficiente."))
        };
        var coordinator = Coordinator(api, store, () => session);

        await coordinator.BeginAsync(Preview());
        Assert.Null(await coordinator.LoadAsync());

        api.Reply = () => Task.FromResult(
            CashierOperationResult<CashierRedemptionResponse>.UncertainFailure("timeout"));
        await coordinator.BeginAsync(Preview());
        api.Reply = () => Task.FromResult(
            CashierOperationResult<CashierRedemptionResponse>.Failure("Saldo insuficiente."));

        Assert.False((await coordinator.RetryAsync()).Succeeded);
        Assert.NotNull(await coordinator.LoadAsync());
        Assert.Equal("Primero resuelve el descuento pendiente.", (await coordinator.BeginAsync(Preview())).ErrorMessage);
    }

    [Fact]
    public async Task Concurrent_monetary_submissions_do_not_send_twice()
    {
        var tcs = new TaskCompletionSource<CashierOperationResult<CashierRedemptionResponse>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new FakeApi { Reply = () => tcs.Task };
        var session = Session();
        var coordinator = Coordinator(api, new MemoryStore(), () => session);

        var first = coordinator.BeginAsync(Preview());

        Assert.Equal("Ya hay una operación en curso.", (await coordinator.BeginAsync(Preview())).ErrorMessage);
        Assert.Equal("Ya hay una operación en curso.", (await coordinator.RetryAsync()).ErrorMessage);
        tcs.SetResult(CashierOperationResult<CashierRedemptionResponse>.Success(Response()));
        Assert.True((await first).Succeeded);
        Assert.Single(api.Calls);
    }

    private static MonetaryRedemptionCoordinator Coordinator(
        ICashierMonetaryRedemptionApi api,
        MemoryStore store,
        Func<CashierSession?> session) =>
        new(api, store, session, "https://api.example.test");

    private sealed class MemoryStore : IMonetaryRedemptionPendingStore
    {
        public Dictionary<string, string> Data { get; } = [];
        public Task<string?> GetAsync(string key) => Task.FromResult(Data.GetValueOrDefault(key));
        public Task SetAsync(string key, string value)
        {
            Data[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeApi : ICashierMonetaryRedemptionApi
    {
        public List<(CashierMonetaryRedemptionPreview Preview, string IdempotencyKey)> Calls { get; } = [];
        public Func<Task<CashierOperationResult<CashierRedemptionResponse>>> Reply { get; set; } =
            () => Task.FromResult(CashierOperationResult<CashierRedemptionResponse>.Success(Response()));

        public Task<CashierOperationResult<CashierMonetaryRedemptionPreview>> PreviewMonetaryAsync(
            string serialNumber,
            int pointsToRedeem,
            CancellationToken ct = default) =>
            Task.FromResult(CashierOperationResult<CashierMonetaryRedemptionPreview>.Success(Preview()));

        public Task<CashierOperationResult<CashierRedemptionResponse>> RedeemMonetaryAsync(
            CashierMonetaryRedemptionPreview preview,
            string idempotencyKey,
            CancellationToken ct = default)
        {
            Calls.Add((preview, idempotencyKey));
            return Reply();
        }
    }
}
