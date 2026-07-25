namespace LoyaltyCloud.Domain.Exceptions;

/// <summary>
/// El cliente solicitado no existe. Usada en flujos internos donde la ausencia
/// es realmente excepcional (no debería pasar). Para búsquedas normales por
/// serial usar <c>Result&lt;T&gt;.Fail("...")</c>.
/// </summary>
public sealed class CustomerNotFoundException : DomainException
{
    public Guid CustomerId { get; }

    public CustomerNotFoundException(Guid customerId)
        : base("CUSTOMER_NOT_FOUND", $"No existe cliente con Id {customerId}.")
    {
        CustomerId = customerId;
    }
}
