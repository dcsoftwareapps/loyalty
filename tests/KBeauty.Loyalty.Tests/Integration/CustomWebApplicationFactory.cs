using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Infrastructure.Persistence;
using KBeauty.Loyalty.Tests.Integration.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KBeauty.Loyalty.Tests.Integration;

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
                ["Apple:WebServiceURL"] = "https://test.local"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Reemplazar DbContext por InMemory.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(_dbName));

            // Reemplazar servicios externos por fakes en memoria.
            services.RemoveAll<IPassGeneratorService>();
            services.RemoveAll<IApnService>();
            services.RemoveAll<IStorageService>();

            services.AddSingleton<IPassGeneratorService, FakePassGeneratorService>();
            services.AddSingleton<IApnService>(Apn);
            services.AddSingleton<IStorageService>(Storage);
        });
    }

    /// <summary>Crea el schema (aplica HasData seed) — llamar una vez por test class.</summary>
    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
