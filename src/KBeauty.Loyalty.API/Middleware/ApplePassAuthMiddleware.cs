using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Repositories;

namespace KBeauty.Loyalty.API.Middleware;

/// <summary>
/// Valida el header <c>Authorization: ApplePass &lt;authToken&gt;</c> que Apple
/// envía en cada request a los endpoints <c>/v1/passes</c> y <c>/v1/devices</c>.
/// El token se compara contra <c>LoyaltyCard.AuthenticationToken</c> del serial
/// presente en la URL.
/// </summary>
/// <remarks>
/// Para el endpoint "listado de updatables" la URL no incluye serial — en ese
/// caso el middleware solo verifica que el header tenga la forma correcta (no
/// puede validar contra una tarjeta puntual).
/// </remarks>
public sealed class ApplePassAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApplePassAuthMiddleware> _logger;

    public ApplePassAuthMiddleware(RequestDelegate next, ILogger<ApplePassAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ILoyaltyCardRepository cards)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!RequiresAuth(path))
        {
            await _next(context);
            return;
        }

        // Header: "ApplePass <token>"
        var authHeader = context.Request.Headers.Authorization.ToString();
        var schemePrefix = LoyaltyConstants.ApplePass.AuthScheme + " ";

        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith(schemePrefix, StringComparison.Ordinal))
        {
            _logger.LogWarning("Apple Pass auth missing/malformed for {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var token = authHeader[schemePrefix.Length..].Trim();

        var serial = ExtractSerialFromPath(path);
        if (serial is null)
        {
            // Endpoints sin serial (listado de updatables) — sin validación contra DB,
            // pero exigimos al menos un header presente para no aceptar requests sin auth.
            await _next(context);
            return;
        }

        var card = await cards.GetBySerialNumberAsync(serial, context.RequestAborted);
        if (card is null || !string.Equals(card.AuthenticationToken, token, StringComparison.Ordinal))
        {
            _logger.LogWarning("Apple Pass auth rejected for serial {Serial}", serial);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }

    private static bool RequiresAuth(string path) =>
        path.StartsWith("/v1/passes/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/v1/devices/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Extrae el serial cuando la URL lo lleva como último segmento:
    /// <list type="bullet">
    ///   <item><c>/v1/passes/{passType}/{serial}</c></item>
    ///   <item><c>/v1/devices/{deviceId}/registrations/{passType}/{serial}</c></item>
    /// </list>
    /// Para <c>/v1/devices/{deviceId}/registrations/{passType}</c> (listado) retorna null.
    /// </summary>
    private static string? ExtractSerialFromPath(string path)
    {
        var segments = path.TrimStart('/').Split('/');

        // /v1/passes/{passType}/{serial}
        if (segments.Length == 4 &&
            segments[0].Equals("v1", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("passes", StringComparison.OrdinalIgnoreCase))
        {
            return segments[3];
        }

        // /v1/devices/{deviceId}/registrations/{passType}/{serial}
        if (segments.Length == 6 &&
            segments[0].Equals("v1", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("devices", StringComparison.OrdinalIgnoreCase) &&
            segments[3].Equals("registrations", StringComparison.OrdinalIgnoreCase))
        {
            return segments[5];
        }

        return null;
    }
}
