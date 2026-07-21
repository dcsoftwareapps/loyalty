namespace LoyaltyCloud.Domain.Exceptions;

/// <summary>
/// Se intentó confirmar (o cancelar) un canje que ya no está en estado Pending.
/// </summary>
public sealed class RedemptionAlreadyConfirmedException : DomainException
{
    public Guid RedemptionId { get; }

    public RedemptionAlreadyConfirmedException(Guid redemptionId)
        : base("REDEMPTION_ALREADY_RESOLVED",
              $"El canje {redemptionId} ya fue confirmado o cancelado.")
    {
        RedemptionId = redemptionId;
    }
}
