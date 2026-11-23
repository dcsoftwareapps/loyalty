extern alias AdminApp;

using System.Reflection;
using System.Security.Claims;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

#pragma warning disable BL0006 // Exercise the real mounted component tree and its lifetime.
[Trait("Category", "StampCashier")]
public sealed class CashierModeRetentionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Web_tab_round_trip_preserves_state_and_pending_prevents_switch(bool uncertain)
    {
        var activator = new TrackingActivator();
        await using var baseFactory = new AdminRoutingTests.AdminWebApplicationFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => {
            services.AddSingleton<IComponentActivator>(activator);
            services.AddScoped<AuthenticationStateProvider, Auth>();
            services.AddScoped<NavigationManager, Navigation>();
            services.AddScoped<ITenantProgramReadService, ProgramReader>();
            services.AddScoped<IJSRuntime, JavaScript>();
        }));
        using var scope = factory.Services.CreateScope();
        await using var renderer = new MountedRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () => {
            var page = await renderer.StartAsync<AdminApp::LoyaltyCloud.Admin.Pages.CashierLanding>();
            var web = Assert.Single(activator.Instances.OfType<AdminApp::LoyaltyCloud.Admin.Shared.WebStampCashier>());
            var panel = Assert.Single(activator.Instances.OfType<AdminApp::LoyaltyCloud.Shared.StampCashierPanel>());
            var api = new Api { Uncertain = uncertain }; var flow = new StampCashierFlow(api, new Store(), "test");
            await flow.LookupAsync("KB-DANIEL");
            Set(web, "flow", flow); Set(page, "giftCardCodeInput", "GC-AAAA-BBBB-CCCC"); Set(page, "giftCardAmountInput", "25");
            await renderer.RefreshAsync();
            await (Task)panel.GetType().GetMethod("EarnAsync", Flags)!.Invoke(panel, [flow.EarnIntentKey])!;
            await renderer.RefreshAsync();
            var pending = flow.Pending;
            var customer = flow.State; var feedback = flow.Message; var intent = flow.EarnIntentKey;
            var surfaceType = page.GetType().GetNestedType("CashierSurface", BindingFlags.NonPublic)!;
            await (Task)page.GetType().GetMethod("SwitchSurfaceAsync", Flags)!.Invoke(page, [Enum.Parse(surfaceType, "GiftCard")])!;
            await renderer.RefreshAsync();
            Assert.Equal(uncertain ? "Points" : "GiftCard", Get(page, "surface")!.ToString());
            await (Task)page.GetType().GetMethod("SwitchSurfaceAsync", Flags)!.Invoke(page, [Enum.Parse(surfaceType, "Points")])!;
            await renderer.RefreshAsync();
            Assert.Same(web, Assert.Single(activator.Instances.OfType<AdminApp::LoyaltyCloud.Admin.Shared.WebStampCashier>()));
            Assert.Same(panel, Assert.Single(activator.Instances.OfType<AdminApp::LoyaltyCloud.Shared.StampCashierPanel>()));
            Assert.Same(flow, Get(web, "flow")); Assert.Same(customer, flow.State); Assert.Equal(uncertain ? 2 : 3, flow.State!.CurrentStamps);
            Assert.Same(pending, flow.Pending);
            Assert.Equal(feedback, flow.Message); Assert.Equal(intent, flow.EarnIntentKey); Assert.Equal(1, api.Earns);
            Assert.Equal("GC-AAAA-BBBB-CCCC", Get(page, "giftCardCodeInput")); Assert.Equal("25", Get(page, "giftCardAmountInput"));
        });
    }

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags)!.SetValue(target,value);
    private static object? Get(object target, string name) => target.GetType().GetField(name, Flags)!.GetValue(target);
    private sealed class TrackingActivator : IComponentActivator
    {
        public List<IComponent> Instances { get; } = [];
        public IComponent CreateInstance(Type type) { var value=(IComponent)System.Activator.CreateInstance(type)!; Instances.Add(value); return value; }
    }
    private sealed class MountedRenderer(IServiceProvider services, ILoggerFactory logger) : Renderer(services,logger)
    {
        private int root;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId, IComponentActivator componentActivator, IComponentRenderMode renderMode) => componentActivator.CreateInstance(componentType);
        protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
        protected override void HandleException(Exception exception) => throw exception;
        public async Task<T> StartAsync<T>() where T:IComponent { var component=(T)InstantiateComponent(typeof(T)); root=AssignRootComponentId(component); await RenderRootComponentAsync(root,ParameterView.Empty); return component; }
        public Task RefreshAsync() => RenderRootComponentAsync(root,ParameterView.Empty);
    }
    private sealed class Auth : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role,"Admin"), new Claim("tenant_id",Guid.NewGuid().ToString()), new Claim("tenant_slug","test")],"test"))));
    }
    private sealed class Navigation : NavigationManager { public Navigation() { Initialize("https://admin.test/","https://admin.test/cashier"); } protected override void NavigateToCore(string uri,bool forceLoad) {} }
    private sealed class ProgramReader : ITenantProgramReadService { public Task<LoyaltyProgramType> GetCurrentAsync(CancellationToken ct=default) => Task.FromResult(LoyaltyProgramType.Stamps); }
    private sealed class JavaScript : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier,object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier,CancellationToken ct,object?[]? args) => InvokeAsync<T>(identifier,args);
    }
    private sealed class Store : IStampPendingStore { public Task<string?> GetAsync(string key)=>Task.FromResult<string?>(null); public Task SetAsync(string key,string value)=>Task.CompletedTask; public Task RemoveAsync(string key)=>Task.CompletedTask; }
    private sealed class Api : IStampCashierApi
    {
        public int Earns;
        public bool Uncertain;
        private CashierStampState State(int count) => new("KB-DANIEL","Daniel Sellos",Guid.NewGuid(),1,count,10,false,[],true);
        public Task<StampCall<CashierStampState>> ReadAsync(string serial)=>Task.FromResult(new StampCall<CashierStampState>(State(2)));
        public Task<StampCall<CashierStampResult>> ExecuteAsync(PendingStampOperation operation) { Earns++; return Task.FromResult(Uncertain ? new StampCall<CashierStampResult>(null, Uncertain:true) : new StampCall<CashierStampResult>(new(State(3),[]))); }
    }
}
