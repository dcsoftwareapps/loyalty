using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;

namespace KBeauty.Loyalty.Application.Devices.Queries.GetUpdatableSerials;

/// <inheritdoc cref="GetUpdatableSerialsQuery"/>
public sealed class GetUpdatableSerialsHandler
    : IRequestHandler<GetUpdatableSerialsQuery, Result<UpdatableSerialsDto>>
{
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IDateTimeProvider _dt;

    public GetUpdatableSerialsHandler(IDeviceRegistrationRepository devices, IDateTimeProvider dt)
    {
        _devices = devices;
        _dt = dt;
    }

    /// <inheritdoc />
    public async Task<Result<UpdatableSerialsDto>> Handle(GetUpdatableSerialsQuery query, CancellationToken ct)
    {
        var serials = await _devices.GetUpdatableSerialsAsync(
            query.DeviceLibraryIdentifier,
            query.PassTypeIdentifier,
            query.PassesUpdatedSince,
            ct);

        return Result.Ok(new UpdatableSerialsDto(serials, _dt.UtcNow));
    }
}
