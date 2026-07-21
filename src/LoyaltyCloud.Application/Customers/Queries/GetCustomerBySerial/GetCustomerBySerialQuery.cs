using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Customers.Queries.GetCustomerBySerial;

/// <summary>Búsqueda de clienta por serial — el caso central al escanear el QR del pase.</summary>
public sealed record GetCustomerBySerialQuery(string SerialNumber) : IRequest<Result<CustomerDetailDto>>;
