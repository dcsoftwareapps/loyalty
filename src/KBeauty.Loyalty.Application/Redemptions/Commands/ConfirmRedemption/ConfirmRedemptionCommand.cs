using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Redemptions.Commands.ConfirmRedemption;

/// <summary>
/// El operador confirma desde el panel admin que entregó el beneficio.
/// Cambia el estado a Confirmed y registra el confirmador + timestamp.
/// </summary>
public sealed record ConfirmRedemptionCommand(
    Guid RedemptionId,
    string OperatorId,
    string? Notes = null) : IRequest<Result>;
