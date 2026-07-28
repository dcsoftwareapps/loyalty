using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using LoyaltyCloud.Tests.Integration.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoyaltyCloud.Tests.Integration;

/// <summary>
/// Factory que reemplaza la persistencia con InMemory y los servicios externos
/// (Wallet, APN, Storage) con fakes. Una DB única por instancia — cada test
/// class que la use vía IClassFixture comparte la misma DB; clases distintas
/// están aisladas.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "KBeautyTests-" + Guid.NewGuid().ToString("N");

    /// <summary>Fakes accesibles para que los tests verifiquen las llamadas.</summary>
    public FakeApnService Apn { get; } = new();
    public FakeStorageService Storage { get; } = new();
    public FakeGoogleWalletClient GoogleWallet { get; } = new();

    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Wallet__UseRealPassSigning", "false");
        Environment.SetEnvironmentVariable("Wallet__UseRealApns", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Provee valores mínimos para que AddInfrastructure no falle en arranque.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(test);Database=Test;",
                ["Azure:KeyVaultUri"] = "",
                ["Azure:BlobStorage:ConnectionString"] = "",
                ["Apple:PassTypeIdentifier"] = "pass.com.kbeautymx.loyalty",
                ["Apple:TeamIdentifier"] = "TESTTEAM01",
                ["Apple:WebServiceURL"] = "https://test.local",
                ["AdminApi:SharedSecret"] = "test-admin-api-shared-secret-with-enough-length",
                ["Wallet:UseRealPassSigning"] = "false",
                ["Wallet:UseRealApns"] = "false",
                ["GoogleWallet:Enabled"] = "true",
                ["GoogleWallet:IssuerId"] = "issuer-test",
                ["GoogleWallet:ClassSuffix"] = "loyalty",
                ["GoogleWallet:ObjectIdPrefix"] = "member",
                ["GoogleWallet:ProgramName"] = "KBeauty Loyalty",
                ["GoogleWallet:IssuerName"] = "KBeauty MX",
                ["GoogleWallet:SaveUrlBase"] = "https://pay.google.com/gp/v/save"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Reemplazar DbContext por InMemory.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(_dbName));

            // Reemplazar servicios externos por fakes en memoria.
            services.RemoveAll<IPassGeneratorService>();
            services.RemoveAll<IApnService>();
            services.RemoveAll<IStorageService>();
            services.RemoveAll<IGoogleWalletClient>();
            services.RemoveAll<IGoogleWalletCredentialsProvider>();

            services.AddSingleton<IPassGeneratorService, FakePassGeneratorService>();
            services.AddSingleton<IApnService>(Apn);
            services.AddSingleton<IStorageService>(Storage);
            services.AddSingleton<IGoogleWalletClient>(GoogleWallet);
            services.AddSingleton<IGoogleWalletCredentialsProvider, FakeGoogleWalletCredentialsProvider>();
        });
    }

    /// <summary>Crea el schema (aplica HasData seed) — llamar una vez por test class.</summary>
    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<IMutableTenantContext>()
            .SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await IntegrationTestSeed.EnsureKBeautyPlatformRowsAsync(db);
        await IntegrationTestSeed.EnsureDefaultTenantLevelsAsync(db);
    }
}
