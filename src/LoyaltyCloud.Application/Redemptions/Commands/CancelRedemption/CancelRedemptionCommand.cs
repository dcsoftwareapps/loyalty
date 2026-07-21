using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Redemptions.Commands.CancelRedemption;

/// <summary>Cancela un canje pendiente y restaura los puntos descontados.</summary>
public sealed record CancelRedemptionCommand(
    Guid RedemptionId,
    string OperatorId,
    string? Notes = null) : IRequest<Result<CancelRedemptionResponse>>;
