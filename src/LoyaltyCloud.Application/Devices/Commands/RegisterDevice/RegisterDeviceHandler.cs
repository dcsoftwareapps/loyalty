using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Devices.Commands.RegisterDevice;

/// <inheritdoc cref="RegisterDeviceCommand"/>
public sealed class RegisterDeviceHandler
    : IRequestHandler<RegisterDeviceCommand, Result<RegisterDeviceResponse>>
{
    private readonly IDeviceRegistrationRepository _devices;
    private readonly ILoyaltyCardRepository _cards;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;

    public RegisterDeviceHandler(
        IDeviceRegistrationRepository devices,
        ILoyaltyCardRepository cards,
        IDateTimeProvider dt,
        IUnitOfWork uow)
    {
        _devices = devices;
        _cards = cards;
        _dt = dt;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<Result<RegisterDeviceResponse>> Handle(RegisterDeviceCommand command, CancellationToken ct)
    {
        // Verifica que el serial exista (Apple no debería pedir registrar uno inexistente,
        // pero un 404 explícito es mejor que silenciar).
        var card = await _cards.GetBySerialNumberAsync(command.SerialNumber, ct);
        if (card is null)
            return Result.Fail<RegisterDeviceResponse>("Serial no encontrado.");

        var existing = await _devices.GetAsync(
            command.DeviceLibraryIdentifier,
            command.PassTypeIdentifier,
            command.SerialNumber, ct);

        if (existing is not null)
        {
            // Apple puede rotar el token sin re-registrar — actualizamos por si cambió.
            existing.UpdatePushToken(command.PushToken);
            _devices.Update(existing);
            await _uow.SaveChangesAsync(ct);
            return Result.Ok(new RegisterDeviceResponse(WasNew: false));
        }

        var registration = new DeviceRegistration(
            id: Guid.NewGuid(),
            tenantId: card.TenantId,
            deviceLibraryIdentifier: command.DeviceLibraryIdentifier,
            passTypeIdentifier: command.PassTypeIdentifier,
            serialNumber: command.SerialNumber,
            pushToken: command.PushToken,
            createdAtUtc: _dt.UtcNow);

        await _devices.AddAsync(registration, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(new RegisterDeviceResponse(WasNew: true));
    }
}
