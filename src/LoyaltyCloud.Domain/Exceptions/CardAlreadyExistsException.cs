namespace LoyaltyCloud.Domain.Exceptions;

/// <summary>
/// Se intentó registrar una clienta con un email que ya tiene tarjeta.
/// </summary>
public sealed class CardAlreadyExistsException : DomainException
{
    public string Email { get; }

    public CardAlreadyExistsException(string email)
        : base("CARD_ALREADY_EXISTS", $"Ya existe una tarjeta asociada al email {email}.")
    {
        Email = email;
    }
}
