using KBeauty.Loyalty.Application.Common.Events;
using KBeauty.Loyalty.Domain.Common;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Events;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core de la aplicación. Implementa <see cref="IUnitOfWork"/>
/// directamente — el override de <see cref="SaveChangesAsync(CancellationToken)"/>
/// despacha domain events después del commit.
/// </summary>
public class AppDbContext : DbContext, IUnitOfWork
{
    private readonly IPublisher? _publisher;

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoyaltyCard> LoyaltyCards => Set<LoyaltyCard>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<PointLot> PointLots => Set<PointLot>();
    public DbSet<PointLotConsumption> PointLotConsumptions => Set<PointLotConsumption>();
    public DbSet<Redemption> Redemptions => Set<Redemption>();
    public DbSet<RewardCatalogItem> RewardCatalogItems => Set<RewardCatalogItem>();
    public DbSet<PointCampaign> PointCampaigns => Set<PointCampaign>();
    public DbSet<ProgramConfig> ProgramConfigs => Set<ProgramConfig>();
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<LoyaltyNotification> LoyaltyNotifications => Set<LoyaltyNotification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>Sobrecarga usada en runtime — <see cref="IPublisher"/> se resuelve por DI.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options, IPublisher publisher) : base(options)
    {
        _publisher = publisher;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica todas las IEntityTypeConfiguration<T> del assembly (Configurations/*.cs).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Ignora la lista de domain events en CUALQUIER entidad — es estado in-memory,
        // nunca debe persistirse.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Ignore(nameof(Entity.DomainEvents));
            }
        }

        // Seed de la configuración del programa con sus valores default.
        ProgramConfigSeed.Apply(modelBuilder);
    }

    /// <summary>
    /// Override que despacha domain events DESPUÉS de un commit exitoso.
    /// El orden importa: si <c>base.SaveChangesAsync</c> falla, los eventos NO se
    /// publican — no quedamos con "level upgraded" notificado sin estar persistido.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Snapshot de los eventos antes del commit — en el commit EF puede limpiar
        // el ChangeTracker en ciertos casos.
        var entitiesWithEvents = ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var pendingEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        var rowsAffected = await base.SaveChangesAsync(cancellationToken);

        // Limpia para evitar republicar si la entidad sigue tracked en otra unidad de trabajo.
        foreach (var entity in entitiesWithEvents)
            entity.ClearDomainEvents();

        if (_publisher is not null && pendingEvents.Count > 0)
        {
            foreach (var domainEvent in pendingEvents)
            {
                var notification = WrapAsNotification(domainEvent);
                await _publisher.Publish(notification, cancellationToken);
            }
        }

        return rowsAffected;
    }

    /// <summary>
    /// Envuelve un <see cref="IDomainEvent"/> en su <c>DomainEventNotification&lt;T&gt;</c>
    /// correspondiente — necesario porque MediatR despacha por tipo concreto.
    /// </summary>
    private static INotification WrapAsNotification(IDomainEvent domainEvent)
    {
        var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
        return (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
    }
}
