using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Customers.Commands.RegisterCustomer;

/// <summary>
/// Alta de una clienta nueva: crea <c>Customer</c>, <c>LoyaltyCard</c>,
/// suma bono de bienvenida, suma bono al referidor si aplica, y devuelve
/// el serial + URL para descargar el pase de Wallet.
/// </summary>
public sealed record RegisterCustomerCommand(
    string FullName,
    string Email,
    DateTime DateOfBirth,
    string? Phone = null,
    string? ReferredBySerialNumber = null) : IRequest<Result<RegisterCustomerResponse>>;
