using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Devices.Commands.UnregisterDevice;

/// <summary>Apple llama DELETE cuando el usuario quita el pase de Wallet.</summary>
public sealed record UnregisterDeviceCommand(
    string DeviceLibraryIdentifier,
    string PassTypeIdentifier,
    string SerialNumber) : IRequest<Result>;
