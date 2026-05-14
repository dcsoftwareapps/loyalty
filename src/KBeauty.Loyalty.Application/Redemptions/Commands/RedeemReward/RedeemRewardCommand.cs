using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Redemptions.Commands.RedeemReward;

/// <summary>
/// La clienta inicia el canje de un beneficio. Crea el <c>Redemption</c> en
/// estado Pending, descuenta los puntos del saldo y dispara push a Wallet.
/// El operador lo confirma luego desde el panel admin.
/// </summary>
public sealed record RedeemRewardCommand(
    string SerialNumber,
    Guid RewardCatalogItemId,
    string OperatorId) : IRequest<Result<RedemptionResponse>>;
