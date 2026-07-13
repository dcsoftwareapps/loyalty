using KBeauty.Loyalty.Application.Customers.Queries.GetCustomerDetail;

namespace KBeauty.Loyalty.Application.Common.Interfaces;

/// <summary>Lectura agregada de detalle de cliente para Admin.</summary>
public interface ICustomerDetailReadService
{
    Task<CustomerDetailDto?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
}
