using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Devices.Commands.RegisterDevice;

/// <summary>
/// Apple llama POST /v1/devices/.../registrations/... cuando un iPhone
/// agrega el pase a Wallet. Persiste el DeviceRegistration para enviar pushes.
/// </summary>
/// <param name="DeviceLibraryIdentifier">Identificador de biblioteca enviado por Apple Wallet.</param>
/// <param name="PassTypeIdentifier">Pass Type ID del pase instalado.</param>
/// <param name="SerialNumber">Serial del pase instalado.</param>
/// <param name="PushToken">Token APNs enviado por Apple Wallet.</param>
public sealed record RegisterDeviceCommand(
    string DeviceLibraryIdentifier,
    string PassTypeIdentifier,
    string SerialNumber,
    string PushToken) : IRequest<Result<RegisterDeviceResponse>>;

/// <param name="WasNew">True si el registro fue creado; false si ya existia.</param>
public sealed record RegisterDeviceResponse(bool WasNew);
