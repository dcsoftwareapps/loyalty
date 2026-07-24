using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Security;

namespace LoyaltyCloud.API.Middleware;

public sealed class AdminApiAuthenticationMiddleware
{
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(5);

    private readonly RequestDelegate _next;
    private readonly ILogger<AdminApiAuthenticationMiddleware> _logger;

    public AdminApiAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<AdminApiAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IConfiguration configuration,
        IPublicTenantResolver tenantResolver,
        IMutableTenantContext tenantContext)
    {
        if (!RequiresAdminApiAuthentication(context.Request))
        {
            await _next(context);
            return;
        }

        var sharedSecret = configuration["AdminApi:SharedSecret"];
        if (string.IsNullOrWhiteSpace(sharedSecret))
        {
            _logger.LogError("Admin API request rejected because AdminApi:SharedSecret is not configured.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        var tenantSlug = context.Request.Headers[AdminApiSignature.TenantSlugHeader].ToString();
        var operatorId = context.Request.Headers[AdminApiSignature.OperatorHeader].ToString();
        var timestamp = context.Request.Headers[AdminApiSignature.TimestampHeader].ToString();
        var signature = context.Request.Headers[AdminApiSignature.SignatureHeader].ToString();

        if (string.IsNullOrWhiteSpace(tenantSlug)
            || string.IsNullOrWhiteSpace(operatorId)
            || string.IsNullOrWhiteSpace(timestamp)
            || string.IsNullOrWhiteSpace(signature))
        {
            Reject(context, "missing_headers", tenantSlug);
            return;
        }

        if (!DateTimeOffset.TryParse(timestamp, out var timestampUtc)
            || timestampUtc.Offset != TimeSpan.Zero
            || DateTimeOffset.UtcNow - timestampUtc > AllowedClockSkew
            || timestampUtc - DateTimeOffset.UtcNow > AllowedClockSkew)
        {
            Reject(context, "invalid_timestamp", tenantSlug);
            return;
        }

        context.Request.EnableBuffering();
        using var ms = new MemoryStream();
        await context.Request.Body.CopyToAsync(ms, context.RequestAborted);
        var body = ms.ToArray();
        context.Request.Body.Position = 0;

        var pathAndQuery = context.Request.Path + context.Request.QueryString.ToString();
        if (!AdminApiSignature.VerifySignature(
                sharedSecret,
                context.Request.Method,
                pathAndQuery,
                timestamp,
                tenantSlug,
                operatorId,
                body,
                signature))
        {
            Reject(context, "invalid_signature", tenantSlug);
            return;
        }

        var tenant = await tenantResolver.ResolveBySlugAsync(tenantSlug, context.RequestAborted);
        if (tenant is null)
        {
            Reject(context, "tenant_not_found", tenantSlug, StatusCodes.Status404NotFound);
            return;
        }

        if (!tenant.IsOperational)
        {
            Reject(context, "tenant_not_operational", tenant.Slug, StatusCodes.Status403Forbidden);
            return;
        }

        tenantContext.SetTenant(tenant.TenantId, tenant.Slug);
        context.Request.Headers["X-Operator-Id"] = operatorId;

        _logger.LogInformation(
            "Admin API request authenticated. path={Path}, tenant={TenantSlug}, operator={OperatorId}.",
            context.Request.Path,
            tenant.Slug,
            operatorId);

        await _next(context);
    }

    private static bool RequiresAdminApiAuthentication(HttpRequest request) =>
        request.Path.Equals("/api/points", StringComparison.OrdinalIgnoreCase)
        || request.Path.StartsWithSegments("/api/custom-notification-campaigns", StringComparison.OrdinalIgnoreCase)
        || request.Path.StartsWithSegments("/api/redemptions", StringComparison.OrdinalIgnoreCase)
        || (HttpMethods.IsGet(request.Method)
            && request.Path.StartsWithSegments("/api/customers", StringComparison.OrdinalIgnoreCase));

    private void Reject(HttpContext context, string reason, string? tenantSlug, int status = StatusCodes.Status401Unauthorized)
    {
        _logger.LogWarning(
            "Admin API request rejected. path={Path}, tenant={TenantSlug}, reason={Reason}.",
            context.Request.Path,
            string.IsNullOrWhiteSpace(tenantSlug) ? "<missing>" : tenantSlug,
            reason);

        context.Response.StatusCode = status;
    }
}
