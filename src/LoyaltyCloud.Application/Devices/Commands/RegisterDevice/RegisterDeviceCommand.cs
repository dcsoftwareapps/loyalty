using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Devices.Commands.RegisterDevice;

/// <summary>
/// Apple llama POST /v1/devices/.../registrations/... cuando un iPhone
/// agrega el pase a Wallet. Persiste el <c>DeviceRegistration</c> para
/// poder enviarle pushes después.
/// </summary>
/// <param name="WasNew">Hint para el controller — true responde 201, false responde 200.</param>
public sealed record RegisterDeviceCommand(
    string DeviceLibraryIdentifier,
    string PassTypeIdentifier,
    string SerialNumber,
    string PushToken) : IRequest<Result<RegisterDeviceResponse>>;

public sealed record RegisterDeviceResponse(bool WasNew);
