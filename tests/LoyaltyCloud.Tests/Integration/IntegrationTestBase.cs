using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

/// <summary>
/// Base para tests de integración. Hereda y usa <see cref="Client"/> + helpers
/// para hidratar la DB con datos de prueba (rewards, etc.).
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    protected CustomWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>xUnit corre esto antes de cada test class — inicializa schema + seed extra.</summary>
    public virtual async Task InitializeAsync()
    {
        await Factory.EnsureDatabaseCreatedAsync();
        await SeedAdditionalDataAsync();
    }

    public virtual Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Inserta RewardCatalogItem para que existan ítems canjeables en los tests.
    /// El seed de OnModelCreating solo cubre ProgramConfig.
    /// </summary>
    private async Task SeedAdditionalDataAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.RewardCatalogItems.Any()) return;

        db.RewardCatalogItems.Add(new RewardCatalogItem(
            Guid.Parse("c0000001-0000-0000-0000-000000000001"),
            "Mini producto",
            "Mini producto de regalo en tienda",
            pointsCost: 300,
            minLevel: LoyaltyConstants.Levels.Mist));

        db.RewardCatalogItems.Add(new RewardCatalogItem(
            Guid.Parse("c0000001-0000-0000-0000-000000000002"),
            "$50 off",
            "Descuento de $50 MXN en compra",
            pointsCost: 500,
            minLevel: LoyaltyConstants.Levels.Mist));

        await db.SaveChangesAsync();
    }
}
