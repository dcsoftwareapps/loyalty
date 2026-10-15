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
        Assert.DoesNotContain("Cancelar último sello", html);
        Assert.DoesNotContain("Monto de compra", html);
        Assert.DoesNotContain("Canjear puntos", html);
    }

    [Fact]
    public async Task Earn_result_keeps_manual_earn_enabled_and_only_explicit_scan_reopens_camera()
    {
        var state = new CashierStampState("KB-TEST", "Daniel", Guid.NewGuid(), 1, 10, 10, true, [], true);
        var flow = new StampCashierFlow(new Api(state), new Store(), "a");
        await flow.AcceptScanAsync("KB-TEST");
        var html = System.Net.WebUtility.HtmlDecode(await RenderAsync(flow));
        Assert.Contains("10 de 10 sellos", html);
        Assert.Contains("¡Tarjeta completada!", html);
        Assert.Contains("Escanear cliente", html); Assert.DoesNotContain("Escanear otro cliente", html);
        Assert.Contains("Cancelar último sello", html);
        Assert.Contains("Agregar sello", html);
        Assert.DoesNotMatch("<button[^>]*disabled[^>]*>Agregar sello</button>", html);
        Assert.DoesNotContain("<strong>0 de 10 sellos", html);
    }

    public static IEnumerable<object[]> Totals() => Enumerable.Range(2, 11).SelectMany(n => new[] { new object[] { n, false }, new object[] { n, true } });

    [Theory, MemberData(nameof(Totals))]
    public async Task Progress_wraps_and_empty_rewards_have_no_heading(int total, bool mobile)
    {
        var state = new CashierStampState("KB-TEST", "Ana", Guid.NewGuid(), 1, total - 1, total, false, []);
        var flow = new StampCashierFlow(new Api(state), new Store(), "a"); await flow.LookupAsync("KB-TEST");
        var html = await RenderAsync(flow, mobile);
        Assert.Equal(total, System.Text.RegularExpressions.Regex.Matches(html, "class=\"stamp-dot").Count);
        Assert.DoesNotContain("<h3>Recompensas</h3>", html);
        Assert.DoesNotContain("Recompensas disponibles", html);
    }

    [Fact]
    public async Task Upcoming_used_and_unlocked_rewards_render_without_main_state_labels()
    {
        var reward = new CashierStampReward(Guid.NewGuid(), "Café", null, 5, DateTime.UtcNow);
        var state = new CashierStampState("KB-TEST", "Ana", Guid.NewGuid(), 1, 5, 10, false, [reward], true,
            [new(Guid.NewGuid(), "Bebida", null, 10, false), new(Guid.NewGuid(), "Galleta", null, 3, true)]);
        var flow = new StampCashierFlow(new Api(state, [reward]), new Store(), "a"); await flow.AcceptScanAsync("KB-TEST");
        var html = System.Net.WebUtility.HtmlDecode(await RenderAsync(flow));
        Assert.Contains("¡Recompensa desbloqueada!", html); Assert.Contains("Café", html);
        Assert.Contains("Al llegar a 10 sellos", html); Assert.Contains("Ya utilizada", html);
        Assert.Contains("stamp-reward available", html); Assert.Contains("Canjear", html);
        Assert.DoesNotContain("Bloqueada", html); Assert.DoesNotContain("Canjeada", html);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Initial_scan_and_manual_search_are_separate(bool mobile)
    {
        var state = new CashierStampState("KB-TEST", "Daniel", Guid.NewGuid(), 1, 2, 10, false, []);
        var html = System.Net.WebUtility.HtmlDecode(await RenderAsync(new StampCashierFlow(new Api(state), new Store(), "a"), mobile));
        Assert.Contains("stamp-scan-entry", html); Assert.Contains("stamp-manual-entry", html);
        Assert.True(html.IndexOf("Escanear cliente", StringComparison.Ordinal) < html.IndexOf("Buscar cliente manualmente", StringComparison.Ordinal));
        Assert.Contains("ID del cliente", html); Assert.DoesNotContain("stamp-customer-label", html.Split("<style>")[0]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Earn_feedback_follows_earn_and_reset_precedes_secondary_reversal(bool mobile)
    {
        var state = new CashierStampState("KB-TEST", "Daniel Sellos", Guid.NewGuid(), 1, 2, 10, false, [], true);
        var flow = new StampCashierFlow(new Api(state), new Store(), "a"); await flow.LookupAsync("KB-TEST"); await flow.BeginAsync("Earn");
        var html = System.Net.WebUtility.HtmlDecode(await RenderAsync(flow, mobile));
        Assert.Contains("CLIENTE", html); Assert.Contains("Daniel Sellos", html); Assert.Contains("role=\"status\"", html);
        Assert.True(html.IndexOf("Agregar sello", StringComparison.Ordinal) < html.IndexOf("Sello agregado", StringComparison.Ordinal));
        Assert.True(html.IndexOf("Sello agregado", StringComparison.Ordinal) < html.IndexOf("Escanear cliente", StringComparison.Ordinal));
        Assert.True(html.IndexOf("Escanear cliente", StringComparison.Ordinal) < html.IndexOf("Cancelar último sello", StringComparison.Ordinal));
        Assert.DoesNotContain("<h3 role=\"status\">", html);
        Assert.Matches(mobile ? "<button[^>]*secondary-button[^>]*>Cancelar último sello</button>" : "<button[^>]*kb-btn--ghost[^>]*>Cancelar último sello</button>", html);
    }

    [Fact]
    public async Task Unlock_uses_historical_short_name_and_keeps_one_redeemable_reward()
    {
        var reward = new CashierStampReward(Guid.NewGuid(), "Nombre largo histórico", null, 5, DateTime.UtcNow);
        var state = new CashierStampState("KB-TEST", "Daniel", Guid.NewGuid(), 1, 5, 10, false, [reward]);
        var flow = new StampCashierFlow(new Api(state, [reward]), new Store(), "a"); await flow.AcceptScanAsync("KB-TEST");
        var html = System.Net.WebUtility.HtmlDecode(await RenderAsync(flow, shortNames: new Dictionary<Guid,string> { [reward.Id] = "Café gratis" }));
        Assert.Contains("Café gratis", html); Assert.Contains("¡Recompensa desbloqueada!", html); Assert.DoesNotContain("Sello agregado", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, ">Canjear</button>").Cast<System.Text.RegularExpressions.Match>());
        Assert.True(html.IndexOf("Agregar sello", StringComparison.Ordinal) < html.IndexOf("¡Recompensa desbloqueada!", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Accumulated_reward_at_two_of_ten_is_available_but_current_future_rewards_have_no_redeem_button()
    {
        var reward = new CashierStampReward(Guid.NewGuid(), "R2", null, 10, DateTime.UtcNow.AddDays(-1));
        var state = new CashierStampState("KB-TEST", "Daniel", Guid.NewGuid(), 2, 2, 10, false, [reward], false,
            [new(Guid.NewGuid(), "R1", null, 5, false), new(Guid.NewGuid(), "R2", null, 10, false)]);
        var flow = new StampCashierFlow(new Api(state), new Store(), "a"); await flow.LookupAsync("KB-TEST");
        var html = System.Net.WebUtility.HtmlDecode(await RenderAsync(flow));
        Assert.Contains("2 de 10 sellos", html); Assert.Contains("Disponible", html); Assert.Contains("Al llegar a 5 sellos", html); Assert.Contains("Al llegar a 10 sellos", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, ">Canjear</button>").Cast<System.Text.RegularExpressions.Match>());
        Assert.DoesNotContain("Cancelar último sello", html);
    }

    [Fact]
    public async Task Scan_action_clears_selected_customer_and_feedback_before_opening_scanner()
    {
        var state = new CashierStampState("KB-TEST", "Daniel", Guid.NewGuid(), 1, 2, 10, false, []);
        var flow = new StampCashierFlow(new Api(state), new Store(), "a"); await flow.AcceptScanAsync("KB-TEST");
        var panel = new AdminApp::LoyaltyCloud.Shared.StampCashierPanel();
        var opened = false;
        ParameterView.FromDictionary(new Dictionary<string,object?> {
            ["Flow"] = flow, ["ParseSerial"] = (Func<string?,string?>)(v => v),
            ["Scan"] = (Func<Task<StampScanResult>>)(() => { Assert.Null(flow.State); Assert.Null(flow.Message); Assert.Empty(flow.Unlocked); opened = true; return Task.FromResult(new StampScanResult(null)); })
        }).SetParameterProperties(panel);
        var method = panel.GetType().GetMethod("ScanAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        await (Task)method.Invoke(panel, null)!; Assert.True(opened); Assert.False(flow.ScanConsumed);
    }

    [Fact]
    public async Task Manual_customer_change_does_not_open_scanner_and_clears_customer_specific_ui()
    {
        var reward = new CashierStampReward(Guid.NewGuid(), "Reward", null, 5, DateTime.UtcNow);
        var state = new CashierStampState("KB-TEST", "Daniel", Guid.NewGuid(), 1, 5, 10, false, [reward], true);
        var flow = new StampCashierFlow(new Api(state, [reward]), new Store(), "a"); await flow.AcceptScanAsync("KB-TEST");
        var panel = new AdminApp::LoyaltyCloud.Shared.StampCashierPanel(); var opened = false;
        ParameterView.FromDictionary(new Dictionary<string,object?> { ["Flow"] = flow,
            ["ParseSerial"] = (Func<string?,string?>)(v => v), ["Scan"] = (Func<Task<StampScanResult>>)(() => { opened=true; return Task.FromResult(new StampScanResult(null)); }) }).SetParameterProperties(panel);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        panel.GetType().GetField("confirmation", flags)!.SetValue(panel, "Redeem");
        panel.GetType().GetField("rewardId", flags)!.SetValue(panel, reward.Id);
        await (Task)panel.GetType().GetMethod("SearchAnotherAsync", flags)!.Invoke(panel, null)!;
        Assert.False(opened); Assert.Null(flow.State); Assert.Null(flow.Message); Assert.Empty(flow.Unlocked);
        Assert.Null(panel.GetType().GetField("confirmation", flags)!.GetValue(panel)); Assert.Null(panel.GetType().GetField("rewardId", flags)!.GetValue(panel));
        var html = System.Net.WebUtility.HtmlDecode(await RenderAsync(flow)); Assert.Contains("Buscar cliente manualmente", html);
        Assert.DoesNotContain("Daniel", html); Assert.DoesNotContain("Cancelar último sello", html);
    }

    private static async Task<string> RenderAsync(StampCashierFlow flow, bool mobile = false, IReadOnlyDictionary<Guid,string>? shortNames = null)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Flow"] = flow,
                ["Mobile"] = mobile,
                ["RewardShortNames"] = shortNames,
                ["Scan"] = (Func<Task<StampScanResult>>)(() => Task.FromResult(new StampScanResult(null))),
                ["ParseSerial"] = (Func<string?, string?>)(value => value)
            });
            var result = await renderer.RenderComponentAsync<AdminApp::LoyaltyCloud.Shared.StampCashierPanel>(parameters);
            return result.ToHtmlString();
        });
    }
    private sealed class Api(CashierStampState state, IReadOnlyList<CashierStampReward>? unlocked = null) : IStampCashierApi
    {
        public Task<StampCall<CashierStampState>> ReadAsync(string serial) => Task.FromResult(new StampCall<CashierStampState>(state));
        public Task<StampCall<CashierStampResult>> ExecuteAsync(PendingStampOperation operation) => Task.FromResult(new StampCall<CashierStampResult>(new(state, unlocked ?? [])));
    }
    private sealed class Store : IStampPendingStore
    {
        public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null);
        public Task SetAsync(string key, string value) => Task.CompletedTask;
        public Task RemoveAsync(string key) => Task.CompletedTask;
    }
}
