using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Customers.Queries.GetCustomerBySerial;

/// <summary>Búsqueda de clienta por serial — el caso central al escanear el QR del pase.</summary>
public sealed record GetCustomerBySerialQuery(string SerialNumber) : IRequest<Result<CustomerDetailDto>>;
