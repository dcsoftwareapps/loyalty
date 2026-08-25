using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Common.Interfaces;

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
    Task<ApnPushResult> SendPassUpdateAsync(string pushToken, PassUpdateReason reason, CancellationToken ct = default);
}

public enum ApnPushFailureType
{
    None = 0,
    Transient = 1,
    Permanent = 2,
    Unsupported = 3
}

public sealed record ApnPushResult(
    bool Success,
    int? StatusCode,
    string? Reason,
    ApnPushFailureType FailureType)
{
    public static ApnPushResult Accepted(int statusCode) =>
        new(true, statusCode, null, ApnPushFailureType.None);

    public static ApnPushResult Transient(int? statusCode, string? reason) =>
        new(false, statusCode, reason, ApnPushFailureType.Transient);

    public static ApnPushResult Permanent(int? statusCode, string? reason) =>
        new(false, statusCode, reason, ApnPushFailureType.Permanent);

    public static ApnPushResult Unsupported(string reason) =>
        new(false, null, reason, ApnPushFailureType.Unsupported);
}
