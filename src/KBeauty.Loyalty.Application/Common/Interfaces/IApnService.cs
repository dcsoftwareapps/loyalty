using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Application.Common.Interfaces;

/// <summary>
/// Cliente de Apple Push Notification para passes. Apple exige un payload vacío
/// <c>{}</c> con topic <c>pass.com.kbeautymx.loyalty</c>; el push solo dispara
/// que Wallet vuelva a llamar el endpoint del pase para refrescar contenido.
/// </summary>
public interface IApnService
{
    /// <summary>
    /// Envía push al token dado. <paramref name="reason"/> se usa solo para
    /// logging/métricas internas — Apple no lo recibe en el payload.
    /// </summary>
    Task SendPassUpdateAsync(string pushToken, PassUpdateReason reason, CancellationToken ct = default);
}
