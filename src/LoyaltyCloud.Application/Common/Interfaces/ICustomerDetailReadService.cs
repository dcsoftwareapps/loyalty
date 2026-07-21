using LoyaltyCloud.Application.Customers.Queries.GetCustomerDetail;

namespace LoyaltyCloud.Application.Common.Interfaces;

/// <summary>Lectura agregada de detalle de cliente para Admin.</summary>
public interface ICustomerDetailReadService
{
    Task<CustomerDetailDto?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
}
