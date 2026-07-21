using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Points.Commands.AddPoints;

/// <summary>
/// Suma puntos a una tarjeta a partir del monto de una compra en tienda física.
/// Aplica x2 si la clienta está en su mes de cumpleaños, recalcula nivel y
/// dispara push de actualización a Wallet.
/// </summary>
public sealed record AddPointsCommand(
    string SerialNumber,
    decimal PurchaseAmount,
    string OperatorId) : IRequest<Result<AddPointsResponse>>;
