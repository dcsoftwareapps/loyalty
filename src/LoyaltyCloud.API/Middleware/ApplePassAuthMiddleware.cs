using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.API.Middleware;

/// <summary>
/// Autentica las llamadas de Apple Wallet que operan sobre un pase concreto.
/// La consulta agrupada de seriales por device admite Authorization opcional,
/// porque representa varios pases y Apple puede enviarla sin token de pase.
/// </summary>
public sealed class ApplePassAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApplePassAuthMiddleware> _logger;

    public ApplePassAuthMiddleware(RequestDelegate next, ILogger<ApplePassAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ILoyaltyCardRepository cards,
        IWalletTenantContextResolver tenantResolver,
        IOptions<ApplePassOptions> options)
    {
        var route = ParseWalletRoute(context.Request.Path.Value ?? string.Empty);
        if (route is null)
        {
            await _next(context);
            return;
        }

        if (!string.Equals(
                route.PassTypeIdentifier,
                options.Value.PassTypeIdentifier,
                StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Apple Wallet request rejected for unknown pass type on {Path}",
                context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (route.IsRegistrationList)
        {
            if (!AuthenticateRegistrationList(context, route))
                return;

            await _next(context);
            return;
        }

        var auth = ReadAuthorization(context);
        if (auth.Status != AuthorizationStatus.Valid)
        {
            LogInvalidAuthorization(context.Request.Path, auth);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var tenant = await tenantResolver.ResolveAndSetTenantAsync(
            route.SerialNumber!,
            requireOperational: true,
            context.RequestAborted);
        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!tenant.IsOperational)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var card = await cards.GetBySerialNumberAsync(route.SerialNumber!, context.RequestAborted);
        if (card is null ||
            !string.Equals(card.AuthenticationToken, auth.Token, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Apple Pass auth rejected for serial {Serial}; headerLength={HeaderLength}, tokenLength={TokenLength}",
                route.SerialNumber,
                auth.HeaderLength,
                auth.TokenLength);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        _logger.LogDebug(
            "Apple Pass auth valid for serial {Serial}; tokenLength={TokenLength}; tenant={TenantSlug}",
            route.SerialNumber,
            auth.TokenLength,
            tenant.TenantSlug);

        await _next(context);
    }

    private bool AuthenticateRegistrationList(HttpContext context, WalletRoute route)
    {
        var auth = ReadAuthorization(context);

        if (auth.Status == AuthorizationStatus.Missing)
        {
            _logger.LogInformation(
                "Apple Wallet registration-list request has no Authorization header; accepted for device {Device} and pass type {PassType}",
                SafeDeviceIdentifier(route.DeviceLibraryIdentifier!),
                route.PassTypeIdentifier);
            return true;
        }

        if (auth.Status == AuthorizationStatus.Malformed)
        {
            LogInvalidAuthorization(context.Request.Path, auth);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return false;
        }

        _logger.LogInformation(
            "Apple Wallet registration-list request has Authorization header; accepted without tenant context. Device={Device}, passType={PassType}, tokenLength={TokenLength}",
            SafeDeviceIdentifier(route.DeviceLibraryIdentifier!),
            route.PassTypeIdentifier,
            auth.TokenLength);
        return true;
    }

    private void LogInvalidAuthorization(PathString path, AuthorizationHeader auth)
    {
        _logger.LogWarning(
            "Apple Pass auth {Status} for {Path}; headerLength={HeaderLength}, tokenLength={TokenLength}",
            auth.Status,
            path,
            auth.HeaderLength,
            auth.TokenLength);
    }

    private static AuthorizationHeader ReadAuthorization(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header))
            return new AuthorizationHeader(AuthorizationStatus.Missing, null, 0, 0);

        var separator = header.IndexOf(' ');
        if (separator <= 0)
            return new AuthorizationHeader(AuthorizationStatus.Malformed, null, header.Length, 0);

        var scheme = header[..separator];
        var token = header[(separator + 1)..].Trim();

        if (!scheme.Equals(LoyaltyConstants.ApplePass.AuthScheme, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(token))
        {
            return new AuthorizationHeader(
                AuthorizationStatus.Malformed,
                null,
                header.Length,
                token.Length);
        }

        return new AuthorizationHeader(
            AuthorizationStatus.Valid,
            token,
            header.Length,
            token.Length);
    }

    private static WalletRoute? ParseWalletRoute(string path)
    {
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 4 &&
            segments[0].Equals("v1", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("passes", StringComparison.OrdinalIgnoreCase))
        {
            return new WalletRoute(
                PassTypeIdentifier: Uri.UnescapeDataString(segments[2]),
                SerialNumber: Uri.UnescapeDataString(segments[3]),
                DeviceLibraryIdentifier: null,
                IsRegistrationList: false);
        }

        if ((segments.Length == 5 || segments.Length == 6) &&
            segments[0].Equals("v1", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("devices", StringComparison.OrdinalIgnoreCase) &&
            segments[3].Equals("registrations", StringComparison.OrdinalIgnoreCase))
        {
            return new WalletRoute(
                PassTypeIdentifier: Uri.UnescapeDataString(segments[4]),
                SerialNumber: segments.Length == 6 ? Uri.UnescapeDataString(segments[5]) : null,
                DeviceLibraryIdentifier: Uri.UnescapeDataString(segments[2]),
                IsRegistrationList: segments.Length == 5);
        }

        return null;
    }

    private static string SafeDeviceIdentifier(string value) =>
        value.Length <= 8 ? value : $"{value[..4]}...{value[^4..]}";

    private enum AuthorizationStatus
    {
        Missing,
        Malformed,
        Valid
    }

    private sealed record AuthorizationHeader(
        AuthorizationStatus Status,
        string? Token,
        int HeaderLength,
        int TokenLength);

    private sealed record WalletRoute(
        string PassTypeIdentifier,
        string? SerialNumber,
        string? DeviceLibraryIdentifier,
        bool IsRegistrationList);
}

