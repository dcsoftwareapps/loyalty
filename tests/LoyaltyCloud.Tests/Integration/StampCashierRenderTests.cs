extern alias AdminApp;

using LoyaltyCloud.Common.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampCashier")]
public sealed class StampCashierRenderTests
{
    [Fact]
    public async Task Shared_panel_renders_grouped_rewards_progress_and_backend_reversal_permission()
    {
        var reward1 = new CashierStampReward(Guid.NewGuid(), "Mascarilla", null, 5, DateTime.UtcNow);
        var reward2 = reward1 with { Id = Guid.NewGuid() };
        var state = new CashierStampState("KB-TEST", "Daniel", Guid.NewGuid(), 1, 7, 10, false, [reward1, reward2]);
        var flow = new StampCashierFlow(new Api(state), new Store(), "a");
        await flow.LookupAsync("KB-TEST");
        var html = await RenderAsync(flow);
        Assert.Contains("Daniel", html);
        Assert.Contains("7 de 10 sellos", html);
        Assert.Contains("Mascarilla", html);
        Assert.Contains("×2", System.Net.WebUtility.HtmlDecode(html));
        Assert.Contains("Agregar sello", html);
        Assert.DoesNotContain("Revertir último sello", html);
        Assert.DoesNotContain("Monto de compra", html);
        Assert.DoesNotContain("Canjear puntos", html);
    }

    [Fact]
    public async Task Earn_result_hides_manual_earn_and_only_explicit_scan_reopens_camera()
    {
        var state = new CashierStampState("KB-TEST", "Daniel", Guid.NewGuid(), 1, 10, 10, true, [], true);
        var flow = new StampCashierFlow(new Api(state), new Store(), "a");
        await flow.AcceptScanAsync("KB-TEST");
        var html = System.Net.WebUtility.HtmlDecode(await RenderAsync(flow));
        Assert.Contains("10 de 10 sellos", html);
        Assert.Contains("¡Tarjeta completada!", html);
        Assert.Contains("Escanear otro cliente", html);
        Assert.Contains("Revertir último sello", html);
        Assert.DoesNotContain("Agregar sello", html);
        Assert.DoesNotContain("<strong>0 de 10 sellos", html);
    }

    private static async Task<string> RenderAsync(StampCashierFlow flow)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Flow"] = flow,
                ["Scan"] = (Func<Task<StampScanResult>>)(() => Task.FromResult(new StampScanResult(null))),
                ["ParseSerial"] = (Func<string?, string?>)(value => value)
            });
            var result = await renderer.RenderComponentAsync<AdminApp::LoyaltyCloud.Shared.StampCashierPanel>(parameters);
            return result.ToHtmlString();
        });
    }
    private sealed class Api(CashierStampState state) : IStampCashierApi
    {
        public Task<StampCall<CashierStampState>> ReadAsync(string serial) => Task.FromResult(new StampCall<CashierStampState>(state));
        public Task<StampCall<CashierStampResult>> ExecuteAsync(PendingStampOperation operation) => Task.FromResult(new StampCall<CashierStampResult>(new(state, [])));
    }
    private sealed class Store : IStampPendingStore
    {
        public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null);
        public Task SetAsync(string key, string value) => Task.CompletedTask;
        public Task RemoveAsync(string key) => Task.CompletedTask;
    }
}
