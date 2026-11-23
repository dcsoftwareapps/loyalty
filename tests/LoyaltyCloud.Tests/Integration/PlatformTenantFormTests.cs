extern alias AdminApp;

using AdminApp::LoyaltyCloud.Admin.Pages;
using AdminApp::LoyaltyCloud.Admin.Services;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Application.SuperAdmin;
using LoyaltyCloud.Application.SuperAdmin.Queries.ListPlatformTenants;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

#pragma warning disable BL0006 // Drive the actual Razor bindings and button handlers without a browser.
public sealed class PlatformTenantFormTests
{
    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Save_uses_text_input_events_even_without_change_or_blur()
    {
        var password = Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid();
        var provisioned = new ProvisionTenantResult(tenantId, "qa-stamps", Guid.NewGuid(), "Trial", DateTime.UnixEpoch);
        ProvisionTenantCommand? sent = null;
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(x => x.Send(It.IsAny<ListPlatformTenantsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlatformTenantListItemDto>());
        sender.Setup(x => x.Send(It.IsAny<ProvisionTenantCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<ProvisionTenantResult>>, CancellationToken>((request, _) =>
                sent = Assert.IsType<ProvisionTenantCommand>(request))
            .ReturnsAsync(Result.Ok(provisioned));

        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(sender.Object)
            .AddSingleton<AdminDateTimeFormatter>()
            .AddSingleton<NavigationManager, TestNavigation>()
            .AddSingleton(Mock.Of<IJSRuntime>())
            .BuildServiceProvider();
        await using var renderer = new FormRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync();
            await renderer.ClickAsync("Crear tenant");
            await renderer.EditAsync("tenant-name", "oninput", "QA Stamps");
            await renderer.EditAsync("tenant-slug", "oninput", "qa-stamps");
            await renderer.EditAsync("tenant-admin", "oninput", "qa-owner");
            await renderer.EditAsync("tenant-password", "oninput", password);
            await renderer.EditAsync("tenant-phone", "oninput", "1234567890");
            await renderer.EditAsync("tenant-program-type", "onchange", nameof(LoyaltyProgramType.Stamps));
            await renderer.ClickAsync("Guardar");
        });

        Assert.NotNull(sent);
        Assert.Equal("QA Stamps", sent.DisplayName);
        Assert.Equal("qa-stamps", sent.Slug);
        Assert.Equal("qa-owner", sent.AdminUsername);
        Assert.True(password == sent.AdminPassword, "The command should receive the initial password.");
        Assert.Equal("1234567890", sent.SupportPhone);
        Assert.Equal(LoyaltyProgramType.Stamps, sent.ProgramType);
        Assert.Equal($"https://admin.test/platform/tenants/{tenantId}", services.GetRequiredService<NavigationManager>().Uri);
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://admin.test/", "https://admin.test/platform/tenants");

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
    }

    private sealed class FormRenderer(IServiceProvider services, ILoggerFactory logger) : Renderer(services, logger)
    {
        private int root;

        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        public Task MountAsync()
        {
            root = AssignRootComponentId(InstantiateComponent(typeof(PlatformTenants)));
            return RenderRootComponentAsync(root, ParameterView.Empty);
        }

        public Task EditAsync(string id, string eventName, string value) =>
            DispatchEventAsync(FindHandler(id, eventName, matchButtonText: false), null, new ChangeEventArgs { Value = value });

        public Task ClickAsync(string text) =>
            DispatchEventAsync(FindHandler(text, "onclick", matchButtonText: true), null, new MouseEventArgs());

        protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;

        protected override void HandleException(Exception exception) =>
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(exception);

        protected override IComponent ResolveComponentForRenderMode(
            Type componentType, int? parentComponentId, IComponentActivator componentActivator, IComponentRenderMode renderMode) =>
            componentActivator.CreateInstance(componentType);

        private ulong FindHandler(string target, string eventName, bool matchButtonText)
        {
            var frames = GetCurrentRenderTreeFrames(root);
            for (var i = 0; i < frames.Count; i++)
            {
                var element = frames.Array[i];
                if (element.FrameType != RenderTreeFrameType.Element)
                    continue;

                var j = i + 1;
                var matches = false;
                ulong handler = 0;
                for (; j < i + element.ElementSubtreeLength && frames.Array[j].FrameType == RenderTreeFrameType.Attribute; j++)
                {
                    var attribute = frames.Array[j];
                    if (attribute.AttributeName == "id" && Equals(attribute.AttributeValue, target))
                        matches = true;
                    if (attribute.AttributeName == eventName)
                        handler = attribute.AttributeEventHandlerId;
                }

                if (matchButtonText)
                    matches = element.ElementName == "button" && j < i + element.ElementSubtreeLength &&
                        frames.Array[j].FrameType == RenderTreeFrameType.Text && frames.Array[j].TextContent == target;

                if (matches)
                {
                    Assert.True(handler != 0, $"{target} has no {eventName} handler.");
                    return handler;
                }
            }

            throw new InvalidOperationException($"Element not found: {target}.");
        }
    }
}
#pragma warning restore BL0006
