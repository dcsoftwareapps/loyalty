namespace LoyaltyCloud.Domain.Common;

public interface ITenantOwned
{
    Guid TenantId { get; }
}
