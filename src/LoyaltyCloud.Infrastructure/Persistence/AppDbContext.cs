using LoyaltyCloud.Application.Common.Events;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Events;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LoyaltyCloud.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core de la aplicación. Implementa <see cref="IUnitOfWork"/>
/// directamente — el override de <see cref="SaveChangesAsync(CancellationToken)"/>
/// despacha domain events después del commit.
/// </summary>
public class AppDbContext : DbContext, IUnitOfWork
{
    private readonly IPublisher? _publisher;
    private readonly ITenantContext? _tenantContext;
    private static readonly Guid MissingTenantId = Guid.Empty;

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoyaltyCard> LoyaltyCards => Set<LoyaltyCard>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<PointLot> PointLots => Set<PointLot>();
    public DbSet<PointLotConsumption> PointLotConsumptions => Set<PointLotConsumption>();
    public DbSet<Redemption> Redemptions => Set<Redemption>();
    public DbSet<RewardCatalogItem> RewardCatalogItems => Set<RewardCatalogItem>();
    public DbSet<PointCampaign> PointCampaigns => Set<PointCampaign>();
    public DbSet<CustomNotificationCampaign> CustomNotificationCampaigns => Set<CustomNotificationCampaign>();
    public DbSet<ProgramConfig> ProgramConfigs => Set<ProgramConfig>();
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<LoyaltyNotification> LoyaltyNotifications => Set<LoyaltyNotification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<TenantAdminUser> TenantAdminUsers => Set<TenantAdminUser>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>Sobrecarga usada en runtime — <see cref="IPublisher"/> se resuelve por DI.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options, IPublisher publisher) : base(options)
    {
        _publisher = publisher;
    }

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IPublisher publisher,
        ITenantContext tenantContext) : base(options)
    {
        _publisher = publisher;
        _tenantContext = tenantContext;
    }

    public Guid CurrentTenantId => _tenantContext?.TenantId ?? MissingTenantId;

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
        TenantSeed.Apply(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Override que despacha domain events DESPUÉS de un commit exitoso.
    /// El orden importa: si <c>base.SaveChangesAsync</c> falla, los eventos NO se
    /// publican — no quedamos con "level upgraded" notificado sin estar persistido.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        GuardTenantOwnedChanges();

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

    public override int SaveChanges()
    {
        GuardTenantOwnedChanges();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardTenantOwnedChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        GuardTenantOwnedChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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

    private void GuardTenantOwnedChanges()
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is ITenantOwned
                     && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0)
            return;

        var tenantId = _tenantContext?.TenantId
            ?? throw new InvalidOperationException(
                "No hay tenant resuelto para guardar cambios en entidades tenant-owned. " +
                "Establece IMutableTenantContext antes de ejecutar operaciones comerciales.");

        foreach (var entry in entries)
        {
            var tenantProperty = entry.Property(nameof(ITenantOwned.TenantId));
            var currentTenantId = (Guid)tenantProperty.CurrentValue!;
            var originalTenantId = entry.State == EntityState.Added
                ? currentTenantId
                : (Guid)tenantProperty.OriginalValue!;

            if (entry.State == EntityState.Added)
            {
                if (currentTenantId == Guid.Empty)
                {
                    tenantProperty.CurrentValue = tenantId;
                    continue;
                }

                if (currentTenantId != tenantId)
                    ThrowTenantMismatch(entry, currentTenantId, tenantId);

                continue;
            }

            if (originalTenantId != tenantId || currentTenantId != tenantId)
                ThrowTenantMismatch(entry, currentTenantId, tenantId);

            if (tenantProperty.IsModified && originalTenantId != currentTenantId)
                throw new InvalidOperationException(
                    $"No se permite modificar TenantId en {entry.Metadata.ClrType.Name}.");
        }
    }

    private static void ThrowTenantMismatch(EntityEntry entry, Guid entityTenantId, Guid currentTenantId)
    {
        throw new InvalidOperationException(
            $"La entidad {entry.Metadata.ClrType.Name} pertenece al tenant {entityTenantId}, " +
            $"pero el tenant actual es {currentTenantId}.");
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        ApplyTenantQueryFilter<TenantAdminUser>(modelBuilder);
        ApplyTenantQueryFilter<Customer>(modelBuilder);
        ApplyTenantQueryFilter<LoyaltyCard>(modelBuilder);
        ApplyTenantQueryFilter<RewardCatalogItem>(modelBuilder);
        ApplyTenantQueryFilter<ProgramConfig>(modelBuilder);
        ApplyTenantQueryFilter<PointCampaign>(modelBuilder);
        ApplyTenantQueryFilter<CustomNotificationCampaign>(modelBuilder);
        ApplyTenantQueryFilter<Redemption>(modelBuilder);
        ApplyTenantQueryFilter<LoyaltyNotification>(modelBuilder);
        ApplyTenantQueryFilter<DeviceRegistration>(modelBuilder);
        ApplyTenantQueryFilter<PointTransaction>(modelBuilder);
        ApplyTenantQueryFilter<PointLot>(modelBuilder);
        ApplyTenantQueryFilter<PointLotConsumption>(modelBuilder);
        ApplyTenantQueryFilter<NotificationDelivery>(modelBuilder);
    }

    private void ApplyTenantQueryFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity => entity.TenantId == CurrentTenantId);
    }
}
