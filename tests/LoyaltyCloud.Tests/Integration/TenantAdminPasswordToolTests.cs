using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using LoyaltyCloud.Infrastructure.Services;
using LoyaltyCloud.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class TenantAdminPasswordToolTests
{
    private static readonly Guid TenantId = TenantSeed.KBeautyTenantId;
    private static readonly Guid BellaTenantId = Guid.Parse("d1000000-0000-0000-0000-000000000002");
    private const string TenantSlug = TenantSeed.KBeautySlug;
    private const string BellaTenantSlug = "bella";
    private const string AdminUsername = "Owner";
    private const string OldPassword = "OldPassword123!";
    private const string NewPassword = "NewPassword123!";
    private const string CreatedAdminUsername = "Cashier";
    private const string CreatedPassword = "CreatedPassword123!";

    [Fact]
    [Trait("Category", "Tools")]
    public async Task Create_tenant_admin_creates_active_user_with_normalized_username_and_hashed_password()
    {
        await using var env = await ToolTestEnvironment.CreateAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await env.WithScopeAsync(sp => sp.GetRequiredService<TenantAdminPasswordTool>()
            .CreateAdminAsync(TenantSlug, "cashier", CreatedPassword, output, error));

        Assert.Equal(0, code);
        Assert.Equal("Tenant admin created successfully for tenant 'kbeauty'." + Environment.NewLine, output.ToString());
        Assert.DoesNotContain(CreatedPassword, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(CreatedPassword, error.ToString(), StringComparison.Ordinal);

        await env.WithScopeAsync(async sp =>
        {
            sp.GetRequiredService<IMutableTenantContext>().SetTenant(TenantId, TenantSlug);
            var db = sp.GetRequiredService<AppDbContext>();
            var admin = await db.TenantAdminUsers.SingleAsync(u => u.TenantId == TenantId && u.NormalizedUsername == TenantAdminUser.NormalizeUsername(CreatedAdminUsername));
            var passwords = sp.GetRequiredService<IPasswordHashingService>();

            Assert.Equal("cashier", admin.Username);
            Assert.Equal("CASHIER", admin.NormalizedUsername);
            Assert.True(admin.IsActive);
            Assert.True(passwords.VerifyPassword(admin.PasswordHash, CreatedPassword));
        });
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task Create_tenant_admin_fails_when_tenant_does_not_exist()
    {
        await using var env = await ToolTestEnvironment.CreateAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await env.WithScopeAsync(sp => sp.GetRequiredService<TenantAdminPasswordTool>()
            .CreateAdminAsync("missing-tenant", CreatedAdminUsername, CreatedPassword, output, error));

        Assert.Equal(1, code);
        Assert.Contains("Tenant no encontrado. TenantSlug=missing-tenant", error.ToString());
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task Create_tenant_admin_fails_for_duplicate_username_in_same_tenant()
    {
        await using var env = await ToolTestEnvironment.CreateAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await env.WithScopeAsync(sp => sp.GetRequiredService<TenantAdminPasswordTool>()
            .CreateAdminAsync(TenantSlug, "owner", CreatedPassword, output, error));

        Assert.Equal(1, code);
        Assert.Contains("Admin ya existe.", error.ToString());

        var count = await env.WithScopeAsync(async sp =>
        {
            sp.GetRequiredService<IMutableTenantContext>().SetTenant(TenantId, TenantSlug);
            return await sp.GetRequiredService<AppDbContext>().TenantAdminUsers.CountAsync(u => u.TenantId == TenantId);
        });
        Assert.Equal(1, count);
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task Create_tenant_admin_allows_same_username_in_another_tenant()
    {
        await using var env = await ToolTestEnvironment.CreateAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await env.WithScopeAsync(sp => sp.GetRequiredService<TenantAdminPasswordTool>()
            .CreateAdminAsync(BellaTenantSlug, "owner", CreatedPassword, output, error));

        Assert.Equal(0, code);
        await env.WithScopeAsync(async sp =>
        {
            sp.GetRequiredService<IMutableTenantContext>().SetTenant(BellaTenantId, BellaTenantSlug);
            var exists = await sp.GetRequiredService<AppDbContext>().TenantAdminUsers
                .AnyAsync(u => u.TenantId == BellaTenantId && u.NormalizedUsername == TenantAdminUser.NormalizeUsername(AdminUsername));
            Assert.True(exists);
        });
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task Create_tenant_admin_requires_password_from_environment()
    {
        await using var env = await ToolTestEnvironment.CreateAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await env.WithScopeAsync(sp => sp.GetRequiredService<TenantAdminPasswordTool>()
            .CreateAdminAsync(TenantSlug, CreatedAdminUsername, null, output, error));

        Assert.Equal(2, code);
        Assert.Contains("Falta LOYALTYCLOUD_ADMIN_PASSWORD.", error.ToString());
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task Create_tenant_admin_rejects_short_password()
    {
        await using var env = await ToolTestEnvironment.CreateAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await env.WithScopeAsync(sp => sp.GetRequiredService<TenantAdminPasswordTool>()
            .CreateAdminAsync(TenantSlug, CreatedAdminUsername, "short", output, error));

        Assert.Equal(2, code);
        Assert.Contains("LOYALTYCLOUD_ADMIN_PASSWORD debe tener al menos 8 caracteres.", error.ToString());
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task Create_tenant_admin_does_not_modify_tenant_data()
    {
        await using var env = await ToolTestEnvironment.CreateAsync();
        var before = await env.PlatformReadAsync(db => db.Tenants.AsNoTracking().SingleAsync(t => t.Id == TenantId));

        var code = await env.WithScopeAsync(sp => sp.GetRequiredService<TenantAdminPasswordTool>()
            .CreateAdminAsync(TenantSlug, CreatedAdminUsername, CreatedPassword, new StringWriter(), new StringWriter()));

        var after = await env.PlatformReadAsync(db => db.Tenants.AsNoTracking().SingleAsync(t => t.Id == TenantId));
        Assert.Equal(0, code);
        Assert.Equal(before.Slug, after.Slug);
        Assert.Equal(before.DisplayName, after.DisplayName);
        Assert.Equal(before.IsActive, after.IsActive);
        Assert.Equal(before.TimeZoneId, after.TimeZoneId);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task Reset_tenant_admin_password_uses_environment_value_and_existing_normalization()
    {
        await using var env = await ToolTestEnvironment.CreateAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await env.WithScopeAsync(sp => sp.GetRequiredService<TenantAdminPasswordTool>()
            .ResetPasswordAsync(TenantSlug, "owner", NewPassword, output, error));

        Assert.Equal(0, code);
        Assert.Contains("Password reset completed.", output.ToString());
        Assert.DoesNotContain(NewPassword, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(NewPassword, error.ToString(), StringComparison.Ordinal);

        await env.WithScopeAsync(async sp =>
        {
            sp.GetRequiredService<IMutableTenantContext>().SetTenant(TenantId, TenantSlug);
            var db = sp.GetRequiredService<AppDbContext>();
            var admin = await db.TenantAdminUsers.SingleAsync(u => u.TenantId == TenantId && u.NormalizedUsername == TenantAdminUser.NormalizeUsername(AdminUsername));
            var passwords = sp.GetRequiredService<IPasswordHashingService>();

            Assert.True(passwords.VerifyPassword(admin.PasswordHash, NewPassword));
            Assert.False(passwords.VerifyPassword(admin.PasswordHash, OldPassword));
            Assert.Equal(AdminUsername, admin.Username);
            Assert.Equal(TenantId, admin.TenantId);
        });
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task Reset_tenant_admin_password_requires_password_from_environment()
    {
        await using var env = await ToolTestEnvironment.CreateAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await env.WithScopeAsync(sp => sp.GetRequiredService<TenantAdminPasswordTool>()
            .ResetPasswordAsync(TenantSlug, AdminUsername, null, output, error));

        Assert.Equal(2, code);
        Assert.Contains("Falta LOYALTYCLOUD_ADMIN_PASSWORD.", error.ToString());
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task Reset_tenant_admin_password_fails_for_missing_admin_without_creating_user()
    {
        await using var env = await ToolTestEnvironment.CreateAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await env.WithScopeAsync(sp => sp.GetRequiredService<TenantAdminPasswordTool>()
            .ResetPasswordAsync(TenantSlug, "missing", NewPassword, output, error));

        Assert.Equal(1, code);
        Assert.Contains("Admin no encontrado.", error.ToString());

        var count = await env.WithScopeAsync(async sp =>
        {
            sp.GetRequiredService<IMutableTenantContext>().SetTenant(TenantId, TenantSlug);
            return await sp.GetRequiredService<AppDbContext>().TenantAdminUsers.CountAsync();
        });
        Assert.Equal(1, count);
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task List_tenant_admins_outputs_only_username_and_status()
    {
        await using var env = await ToolTestEnvironment.CreateAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await env.WithScopeAsync(sp => sp.GetRequiredService<TenantAdminPasswordTool>()
            .ListAdminsAsync(TenantSlug, output, error));

        var text = output.ToString();
        Assert.Equal(0, code);
        Assert.Contains("Username", text);
        Assert.Contains("IsActive", text);
        Assert.Contains(AdminUsername, text);
        Assert.Contains("True", text);
        Assert.DoesNotContain("PasswordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OldPassword, text, StringComparison.Ordinal);
        Assert.DoesNotContain(NewPassword, text, StringComparison.Ordinal);
    }

    private sealed class ToolTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private ToolTestEnvironment(ServiceProvider services)
        {
            _services = services;
        }

        public static async Task<ToolTestEnvironment> CreateAsync()
        {
            var dbName = "LoyaltyCloudTools-" + Guid.NewGuid().ToString("N");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=(test);Database=Test;",
                    ["Azure:KeyVaultUri"] = "",
                    ["Azure:BlobStorage:ConnectionString"] = "",
                    ["Apple:PassTypeIdentifier"] = "pass.com.kbeautymx.loyalty",
                    ["Apple:TeamIdentifier"] = "TESTTEAM01",
                    ["Apple:WebServiceURL"] = "https://api.test",
                    ["Apple:OrganizationName"] = "LoyaltyCloud Test",
                    ["Wallet:UseRealPassSigning"] = "false",
                    ["Wallet:UseRealApns"] = "false"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());
            services.AddScoped<TenantAdminPasswordTool>();

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(dbName));

            var provider = services.BuildServiceProvider(validateScopes: true);
            var env = new ToolTestEnvironment(provider);
            await env.SeedAsync();
            return env;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
            }
            finally
            {
                await _services.DisposeAsync();
            }
        }

        public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
        {
            using var scope = _services.CreateScope();
            return await action(scope.ServiceProvider);
        }

        public async Task WithScopeAsync(Func<IServiceProvider, Task> action)
        {
            using var scope = _services.CreateScope();
            await action(scope.ServiceProvider);
        }

        public async Task<T> PlatformReadAsync<T>(Func<AppDbContext, Task<T>> query)
        {
            using var scope = _services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        private async Task SeedAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            db.Tenants.Add(new Tenant(BellaTenantId, BellaTenantSlug, "Bella", "America/Tijuana", DateTime.UtcNow));
            await db.SaveChangesAsync();

            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(TenantId, TenantSlug);
            var passwords = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
            db.TenantAdminUsers.Add(new TenantAdminUser(
                Guid.Parse("d2000000-0000-0000-0000-000000000001"),
                TenantId,
                AdminUsername,
                passwords.HashPassword(OldPassword),
                DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LoyaltyCloud.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
