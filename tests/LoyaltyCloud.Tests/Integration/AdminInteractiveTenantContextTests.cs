extern alias AdminApp;

using System.Security.Claims;
using AdminApp::LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class AdminInteractiveTenantContextTests
{
    private static readonly Guid KBeautyTenantId = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid BellaTenantId = Guid.Parse("b2000000-0000-0000-0000-000000000001");

    [Fact]
    [Trait("Category", "AdminInteractiveTenantContext")]
    [Trait("Category", "AdminCustomerPoints")]
    public async Task Tenant_admin_circuit_restores_tenant_context_before_commercial_request()
    {
        var tenantContext = new TestTenantContext();
        var behavior = CreateBehavior(tenantContext, CreateTenantPrincipal(KBeautyTenantId, "kbeauty"));

        var resolvedTenantId = await behavior.Handle(
            Unit.Value,
            () => Task.FromResult(tenantContext.RequireTenantId()),
            CancellationToken.None);

        Assert.Equal(KBeautyTenantId, resolvedTenantId);
        Assert.Equal("kbeauty", tenantContext.TenantSlug);
    }

    [Fact]
    [Trait("Category", "AdminInteractiveTenantContext")]
    [Trait("Category", "AdminCustomerPoints")]
    public async Task Tenant_admin_initializer_restores_context_for_direct_service_operations()
    {
        var tenantContext = new TestTenantContext();
        var initializer = CreateInitializer(tenantContext, CreateTenantPrincipal(KBeautyTenantId, "kbeauty"));

        var initialized = await initializer.EnsureTenantContextAsync();
        var direct = new DirectTenantAwareService(tenantContext);

        Assert.True(initialized);
        Assert.Equal(KBeautyTenantId, direct.GetTenantId());
        Assert.Equal("kbeauty", tenantContext.TenantSlug);
    }

    [Fact]
    [Trait("Category", "AdminInteractiveTenantContext")]
    [Trait("Category", "AdminCustomerPoints")]
    public async Task Tenant_admin_circuit_uses_authenticated_claims_not_another_tenant()
    {
        var tenantContext = new TestTenantContext();
        var behavior = CreateBehavior(tenantContext, CreateTenantPrincipal(BellaTenantId, "bella-salon"));

        var resolvedTenantId = await behavior.Handle(
            Unit.Value,
            () => Task.FromResult(tenantContext.RequireTenantId()),
            CancellationToken.None);

        Assert.Equal(BellaTenantId, resolvedTenantId);
        Assert.NotEqual(KBeautyTenantId, resolvedTenantId);
        Assert.Equal("bella-salon", tenantContext.TenantSlug);
    }

    [Fact]
    [Trait("Category", "AdminInteractiveTenantContext")]
    public async Task Separate_circuit_scopes_do_not_share_tenant_context()
    {
        var kbeautyContext = new TestTenantContext();
        var bellaContext = new TestTenantContext();

        await CreateInitializer(kbeautyContext, CreateTenantPrincipal(KBeautyTenantId, "kbeauty"))
            .EnsureTenantContextAsync();
        await CreateInitializer(bellaContext, CreateTenantPrincipal(BellaTenantId, "bella-salon"))
            .EnsureTenantContextAsync();

        Assert.Equal(KBeautyTenantId, kbeautyContext.RequireTenantId());
        Assert.Equal(BellaTenantId, bellaContext.RequireTenantId());
        Assert.Equal("kbeauty", kbeautyContext.TenantSlug);
        Assert.Equal("bella-salon", bellaContext.TenantSlug);
    }

    [Fact]
    [Trait("Category", "AdminInteractiveTenantContext")]
    public async Task Anonymous_circuit_does_not_get_silent_default_tenant()
    {
        var tenantContext = new TestTenantContext();
        var behavior = CreateBehavior(tenantContext, new ClaimsPrincipal(new ClaimsIdentity()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                Unit.Value,
                () => Task.FromResult(tenantContext.RequireTenantId()),
                CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "AdminInteractiveTenantContext")]
    public async Task Super_admin_circuit_does_not_get_tenant_context()
    {
        var tenantContext = new TestTenantContext();
        var initializer = CreateInitializer(tenantContext, CreateSuperAdminPrincipal());

        var initialized = await initializer.EnsureTenantContextAsync();

        Assert.False(initialized);
        Assert.False(tenantContext.HasTenant);
    }

    private static AdminTenantContextBehavior<Unit, Guid> CreateBehavior(
        IMutableTenantContext tenantContext,
        ClaimsPrincipal principal) =>
        new(
            CreateInitializer(tenantContext, principal));

    private static AdminTenantContextInitializer CreateInitializer(
        IMutableTenantContext tenantContext,
        ClaimsPrincipal principal) =>
        new(
            new FixedAuthenticationStateProvider(principal),
            tenantContext,
            NullLogger<AdminTenantContextInitializer>.Instance);

    private static ClaimsPrincipal CreateTenantPrincipal(Guid tenantId, string tenantSlug)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(AdminClaimTypes.Subject, Guid.NewGuid().ToString()),
                new Claim(AdminClaimTypes.TenantId, tenantId.ToString()),
                new Claim(AdminClaimTypes.TenantSlug, tenantSlug),
                new Claim(AdminClaimTypes.Name, "owner")
            ],
            authenticationType: "loyaltycloud.admin.auth");

        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateSuperAdminPrincipal()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(AdminClaimTypes.Subject, Guid.NewGuid().ToString()),
                new Claim(AdminClaimTypes.Name, "platform")
            ],
            authenticationType: "loyaltycloud.platform.auth");

        return new ClaimsPrincipal(identity);
    }

    private sealed class FixedAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _principal;

        public FixedAuthenticationStateProvider(ClaimsPrincipal principal) => _principal = principal;

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_principal));
    }

    private sealed class TestTenantContext : IMutableTenantContext
    {
        public Guid? TenantId { get; private set; }
        public string? TenantSlug { get; private set; }
        public bool HasTenant => TenantId.HasValue && !string.IsNullOrWhiteSpace(TenantSlug);

        public void SetTenant(Guid tenantId, string tenantSlug)
        {
            TenantId = tenantId;
            TenantSlug = tenantSlug.Trim().ToLowerInvariant();
        }

        public void Clear()
        {
            TenantId = null;
            TenantSlug = null;
        }
    }

    private sealed class DirectTenantAwareService
    {
        private readonly ITenantContext _tenantContext;

        public DirectTenantAwareService(ITenantContext tenantContext)
        {
            _tenantContext = tenantContext;
        }

        public Guid GetTenantId() => _tenantContext.RequireTenantId();
    }
}
