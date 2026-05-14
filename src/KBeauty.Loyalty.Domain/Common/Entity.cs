using KBeauty.Loyalty.Domain.Events;

namespace KBeauty.Loyalty.Domain.Common;

/// <summary>
/// Clase base de toda entidad del dominio.
/// Aporta identidad (<see cref="Id"/>) y registro de domain events.
/// </summary>
/// <remarks>
/// Los domain events se acumulan en la entidad hasta que el
/// <c>SaveChangesInterceptor</c> de Infrastructure los publique con MediatR.
/// Después de publicarlos se llama a <see cref="ClearDomainEvents"/>.
/// </remarks>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>Identificador único de la entidad.</summary>
    public Guid Id { get; protected set; }

    /// <summary>Eventos pendientes de publicar generados por la entidad.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Ctor sin parámetros — requerido por EF Core. NO usar desde negocio.</summary>
    protected Entity() { }

    /// <summary>Inicializa una entidad con identidad explícita.</summary>
    protected Entity(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id no puede ser vacío.", nameof(id));
        Id = id;
    }

    /// <summary>Registra un domain event para ser publicado tras persistencia.</summary>
    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Limpia los eventos acumulados — llamada por el dispatcher tras publicarlos.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
