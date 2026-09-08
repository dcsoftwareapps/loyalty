extern alias AdminApp;

using System.Security.Claims;
using AdminApp::LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class TenantAuthorizationRoleTests
{
    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    [Trait("Category", "CashierAuth")]
    public void Tenant_admin_user_defaults_to_admin_role()
    {
        var user = new TenantAdminUser(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "owner",
            "hash",
            DateTime.UtcNow);

        Assert.Equal(TenantUserRole.Admin, user.Role);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    [Trait("Category", "CashierAuth")]
    public void Cashier_role_can_be_represented_on_tenant_admin_user()
    {
        var user = new TenantAdminUser(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "cashier",
            "hash",
            DateTime.UtcNow,
            role: TenantUserRole.Cashier);

        Assert.Equal(TenantUserRole.Cashier, user.Role);
    }

    [Theory]
    [Trait("Category", "TenantAdminAuth")]
    [Trait("Category", "CashierAuth")]
    [InlineData(TenantUserRoles.Admin)]
    [InlineData(TenantUserRoles.Cashier)]
    public async Task Admin_and_cashier_satisfy_cashier_operations_policy(string role)
    {
        var authorized = await AuthorizeAsync(CreateTenantPrincipal(role), TenantAuthorizationPolicies.CashierOperations);

        Assert.True(authorized);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    [Trait("Category", "CashierAuth")]
    public async Task Cashier_does_not_satisfy_tenant_admin_policy()
    {
        var authorized = await AuthorizeAsync(CreateTenantPrincipal(TenantUserRoles.Cashier), TenantAuthorizationPolicies.TenantAdmin);

        Assert.False(authorized);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    [Trait("Category", "CashierAuth")]
    public async Task Admin_satisfies_tenant_admin_policy()
    {
        var authorized = await AuthorizeAsync(CreateTenantPrincipal(TenantUserRoles.Admin), TenantAuthorizationPolicies.TenantAdmin);

        Assert.True(authorized);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    [Trait("Category", "CashierAuth")]
    public void Tenant_admin_user_role_migration_defaults_existing_rows_to_admin()
    {
        var root = GetRepositoryRoot();
        var migration = Directory
            .EnumerateFiles(Path.Combine(root, "src", "LoyaltyCloud.Infrastructure", "Persistence", "Migrations"), "*AddTenantAdminUserRole.cs")
            .Single(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase));
        var source = File.ReadAllText(migration);

        Assert.Contains("table: \"TenantAdminUsers\"", source);
        Assert.Contains("name: \"Role\"", source);
        Assert.Contains("defaultValue: \"Admin\"", source);
    }

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal principal, string policy)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(TenantAuthorization.Configure);
        await using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, resource: null, policyName: policy);
        return result.Succeeded;
    }

    private static ClaimsPrincipal CreateTenantPrincipal(string role)
    {
        var claims = new[]
        {
            new Claim(AdminClaimTypes.Subject, Guid.NewGuid().ToString()),
            new Claim(AdminClaimTypes.TenantId, Guid.NewGuid().ToString()),
            new Claim(AdminClaimTypes.TenantSlug, "tenant"),
            new Claim(AdminClaimTypes.Name, "user"),
            new Claim(ClaimTypes.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            AdminClaimTypes.Name,
            ClaimTypes.Role));
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src")))
            current = current.Parent;

        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
