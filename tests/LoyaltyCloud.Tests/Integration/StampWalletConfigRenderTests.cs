extern alias AdminApp;

using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Application.Config.Queries.GetProgramConfig;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using ConfigPage = AdminApp::LoyaltyCloud.Admin.Pages.Config;
using AdminApiClient = AdminApp::LoyaltyCloud.Admin.Services.AdminApiClient;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "AdminConfigurationCleanup")]
public sealed class StampWalletConfigRenderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reward_image_editor_explains_optional_fallback_and_only_offers_removal_for_custom_art(bool custom)
    {
        var identity = Guid.NewGuid(); const string preview = "data:image/png;base64,test";
        var rules = new StampProgramDto(Guid.NewGuid(), 1, 10, 0, StampReversalPolicy.None, [new(10, "Coffee", null, identity, "Coffee")]);
        var visuals = new Mock<IStampVisualService>();
        visuals.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new StampVisualDto(preview, preview,
            new Dictionary<Guid, string> { [identity] = preview })
        { UpcomingRewardPreviews = new Dictionary<Guid, StampUpcomingRewardPreview> { [identity] = new(preview, custom) } });
        var programs = new Mock<ITenantProgramReadService>();
        programs.Setup(x => x.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(LoyaltyProgramType.Stamps);
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<GetActiveStampProgramQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(rules);
        await using var services = new ServiceCollection().AddLogging().AddSingleton(visuals.Object).AddSingleton(programs.Object)
            .AddSingleton(sender.Object).AddSingleton<AuthenticationStateProvider>(new Auth())
            .AddSingleton(new AdminApp::LoyaltyCloud.Admin.Auth.AdminCurrentUserService(new Microsoft.AspNetCore.Http.HttpContextAccessor()))
            .AddSingleton<Microsoft.JSInterop.IJSRuntime>(Mock.Of<Microsoft.JSInterop.IJSRuntime>()).BuildServiceProvider();
        await using var renderer = new Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure.StaticHtmlRenderer(services, services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        var page = new AdminApp::LoyaltyCloud.Admin.Pages.Stamps();
        foreach (var property in page.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(x => x.GetCustomAttribute<InjectAttribute>() is not null))
            property.SetValue(page, services.GetRequiredService(property.PropertyType));
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = renderer.BeginRenderingComponent(page, ParameterView.Empty); await root.QuiescenceTask;
            return WebUtility.HtmlDecode(root.ToHtmlString());
        });
        Assert.Contains("Imagen disponible", html);
        Assert.Contains("Esta es la imagen que verá el cliente cuando desbloquee la recompensa.", html);
        Assert.Contains("Imagen antes de desbloquear (opcional)", html);
        Assert.Contains("Opcional. Si no agregas una imagen, usaremos la imagen disponible en escala de grises hasta que el cliente desbloquee la recompensa.", html);
        var upcoming = html[html.IndexOf("Imagen antes de desbloquear (opcional)", StringComparison.Ordinal)..];
        Assert.DoesNotContain("Usar predeterminada", upcoming);
        Assert.Equal(custom, upcoming.Contains("Quitar imagen"));
        Assert.Equal(!custom, upcoming.Contains("Vista automática"));
    }

    [Theory]
    [InlineData(LoyaltyProgramType.Points, "CustomerName")]
    [InlineData(LoyaltyProgramType.Points, "Image")]
    [InlineData(LoyaltyProgramType.Stamps, "CustomerName")]
    [InlineData(LoyaltyProgramType.Stamps, "Image")]
    public async Task Primary_content_controls_are_only_visible_for_points(LoyaltyProgramType type, string mode)
    {
        var branding = Branding(mode);
        var programs = new Mock<ITenantProgramReadService>();
        programs.Setup(x => x.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(type);
        var read = new Mock<ITenantBrandingReadService>();
        read.Setup(x => x.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(branding);
        var levels = new Mock<ITenantLoyaltyLevelReadService>();
        levels.Setup(x => x.GetActiveLevelsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<TenantLoyaltyLevelDto>());
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<GetProgramConfigQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<IReadOnlyList<ConfigDto>>([]));
        using var http = new HttpClient(new Transport(branding));
        await using var services = new ServiceCollection().AddLogging()
            .AddSingleton(programs.Object).AddSingleton(read.Object).AddSingleton(levels.Object)
            .AddSingleton(sender.Object).AddSingleton(Mock.Of<ILevelProgressService>())
            .AddSingleton(Api(http)).AddSingleton<Microsoft.JSInterop.IJSRuntime>(Mock.Of<Microsoft.JSInterop.IJSRuntime>())
            .BuildServiceProvider();
        await using var renderer = new Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure.StaticHtmlRenderer(services, services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        var component = new ConfigPage();
        foreach (var property in typeof(ConfigPage).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(x => x.GetCustomAttribute<InjectAttribute>() is not null))
            property.SetValue(component, services.GetRequiredService(property.PropertyType));
        // Render the real instance statically; its production InteractiveServer attribute stays intact.
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = renderer.BeginRenderingComponent(component, ParameterView.Empty);
            await root.QuiescenceTask;
            return System.Net.WebUtility.HtmlDecode(root.ToHtmlString());
        });
        foreach (var text in new[] { "Contenido principal", "Nombre del cliente", "Imagen de portada", "apple-wallet-primary-content" })
        {
            if (type == LoyaltyProgramType.Points) Assert.Contains(text, html);
            else Assert.DoesNotContain(text, html);
        }
        Assert.Equal(type == LoyaltyProgramType.Points && mode == "Image", html.Contains("id=\"wallet-strip\""));
    }

    [Theory]
    [InlineData(LoyaltyProgramType.Points, "CustomerName")]
    [InlineData(LoyaltyProgramType.Points, "Image")]
    [InlineData(LoyaltyProgramType.Stamps, "CustomerName")]
    [InlineData(LoyaltyProgramType.Stamps, "Image")]
    public async Task Saving_stamps_branding_leaves_hidden_primary_content_unchanged(LoyaltyProgramType type, string mode)
    {
        var branding = Branding(mode);
        var transport = new Transport(branding);
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://api.test/") };
        var page = new ConfigPage();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ConfigPage).GetProperty("Api", flags)!.SetValue(page, Api(http));
        void Set(string field, object value) => typeof(ConfigPage).GetField(field, flags)!.SetValue(page, value);
        Set("isPoints", type == LoyaltyProgramType.Points); Set("branding", branding);
        Set("walletBackgroundColor", "#123456"); Set("walletLogoScalePercent", 80);
        Set("appleWalletPrimaryContentMode", mode);
        await (Task)typeof(ConfigPage).GetMethod("SaveWalletBrandingAsync", flags)!.Invoke(page, null)!;
        Assert.NotNull(transport.Request);
        using var json = JsonDocument.Parse(transport.Request!);
        var sentMode = json.RootElement.GetProperty("appleWalletPrimaryContentMode");
        if (type == LoyaltyProgramType.Stamps) Assert.Equal(JsonValueKind.Null, sentMode.ValueKind);
        else Assert.Equal(mode, sentMode.GetString());
        Assert.Equal("#123456", json.RootElement.GetProperty("walletBackgroundColor").GetString());
    }

    private static TenantBrandingInfo Branding(string mode) => new(Guid.NewGuid(), "grid", "Grid", "#123456", "#FFFFFF", null,
        "#123456", "#123456", null, false, 80, mode, "https://example.test/cover.png", true, null, null, null, null);
    private static AdminApiClient Api(HttpClient http)
    {
        var factory = new Mock<IHttpClientFactory>(); factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(http);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AdminApi:SharedSecret"] = "test-only" }).Build();
        return new AdminApiClient(factory.Object, new Auth(), configuration);
    }
    private sealed class Auth : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_slug", "grid"), new Claim("sub", "test")], "test"))));
    }
    private sealed class Transport(TenantBrandingInfo branding) : HttpMessageHandler
    {
        public string? Request;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(branding) };
        }
    }
}
