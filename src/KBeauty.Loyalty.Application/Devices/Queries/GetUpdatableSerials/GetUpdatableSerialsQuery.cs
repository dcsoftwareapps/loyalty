using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Devices.Queries.GetUpdatableSerials;

/// <summary>
/// Apple llama GET /v1/devices/.../registrations/...?passesUpdatedSince=...
/// para preguntar qué pases del dispositivo cambiaron desde un timestamp.
/// </summary>
public sealed record GetUpdatableSerialsQuery(
    string DeviceLibraryIdentifier,
    string PassTypeIdentifier,
    DateTime? PassesUpdatedSince) : IRequest<Result<UpdatableSerialsDto>>;

public sealed record UpdatableSerialsDto(
    IReadOnlyList<string> SerialNumbers,
    DateTime LastUpdated);
