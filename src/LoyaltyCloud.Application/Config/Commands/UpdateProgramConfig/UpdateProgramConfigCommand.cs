using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Config.Commands.UpdateProgramConfig;

/// <summary>
/// Actualiza una o más reglas del programa. El handler valida que cada Key
/// pertenezca a <c>LoyaltyConstants.ConfigKeys</c> antes de hacer upsert.
/// </summary>
public sealed record UpdateProgramConfigCommand(
    IReadOnlyList<ConfigEntry> Entries,
    string UpdatedBy) : IRequest<Result>;
