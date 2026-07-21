namespace LoyaltyCloud.Domain.Events;

/// <summary>
/// Marcador para domain events. Los publicaremos vía MediatR (a través de
/// <c>INotification</c> en la capa Application — no aquí, porque Domain no
/// debe conocer MediatR).
/// </summary>
public interface IDomainEvent
{
}
