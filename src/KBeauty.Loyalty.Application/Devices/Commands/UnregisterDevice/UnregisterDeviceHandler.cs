using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;

namespace KBeauty.Loyalty.Application.Devices.Commands.UnregisterDevice;

/// <inheritdoc cref="UnregisterDeviceCommand"/>
public sealed class UnregisterDeviceHandler : IRequestHandler<UnregisterDeviceCommand, Result>
{
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IUnitOfWork _uow;

    public UnregisterDeviceHandler(IDeviceRegistrationRepository devices, IUnitOfWork uow)
    {
        _devices = devices;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(UnregisterDeviceCommand command, CancellationToken ct)
    {
        var existing = await _devices.GetAsync(
            command.DeviceLibraryIdentifier,
            command.PassTypeIdentifier,
            command.SerialNumber, ct);

        // Idempotente: si no existe, OK igual (Apple puede reintentar).
        if (existing is null) return Result.Ok();

        _devices.Remove(existing);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
