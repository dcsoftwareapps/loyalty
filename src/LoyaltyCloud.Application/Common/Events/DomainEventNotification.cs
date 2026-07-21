using LoyaltyCloud.Domain.Events;
using MediatR;

namespace LoyaltyCloud.Application.Common.Events;

/// <summary>
/// Adaptador que envuelve un <see cref="IDomainEvent"/> del dominio para que
/// pueda viajar por la pipeline de <see cref="INotification"/> de MediatR.
/// Domain no puede depender de MediatR, por eso el wrapper vive aquí.
/// </summary>
/// <remarks>
/// Infrastructure (vía <c>SaveChangesInterceptor</c>) recolecta los
/// <see cref="IDomainEvent"/> de las entidades modificadas, los envuelve en
/// este tipo y los publica después del commit con <c>IPublisher.Publish</c>.
/// </remarks>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
