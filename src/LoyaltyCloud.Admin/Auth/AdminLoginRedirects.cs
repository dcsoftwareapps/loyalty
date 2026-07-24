namespace LoyaltyCloud.Admin.Auth;

public static class AdminLoginRedirects
{
    public static string BuildTenantAwareLoginRedirect(HttpRequest request, string fallbackRedirectUri)
    {
        var path = request.Path.Value ?? string.Empty;
        if (path.Equals("/", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(path))
            return "/platform/login";

        if (path.StartsWith("/platform", StringComparison.OrdinalIgnoreCase))
            return BuildPlatformLoginRedirect(request);

        var tenantSlug = TryReadTenantSlug(path);
        if (tenantSlug is null)
            return "/platform/login";

        var returnUrl = Uri.EscapeDataString(request.PathBase + request.Path + request.QueryString);
        return $"/{tenantSlug}/login?returnUrl={returnUrl}";
    }

    public static string ResolveLoginPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Equals("/", StringComparison.Ordinal))
            return "/platform/login";

        if (path.StartsWith("/platform", StringComparison.OrdinalIgnoreCase))
            return "/platform/login";

        var tenantSlug = TryReadTenantSlug(path);
        return tenantSlug is null
            ? "/platform/login"
            : $"/{tenantSlug}/login";
    }

    public static string BuildPlatformLoginRedirect(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        if (path.Equals("/", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(path))
            return "/platform/login";

        if (path.Equals("/platform/login", StringComparison.OrdinalIgnoreCase))
            return "/platform/login";

        var returnUrl = Uri.EscapeDataString(request.Path + request.QueryString);
        return $"/platform/login?returnUrl={returnUrl}";
    }

    private static string? TryReadTenantSlug(string path)
    {
        var trimmed = path.Trim('/');
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var firstSegment = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstSegment))
            return null;

        if (IsReservedAdminSegment(firstSegment)
            || firstSegment.StartsWith("_", StringComparison.Ordinal)
            || firstSegment.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        return firstSegment;
    }

    private static bool IsReservedAdminSegment(string value) =>
        value.Equals("platform", StringComparison.OrdinalIgnoreCase)
        || value.Equals("login", StringComparison.OrdinalIgnoreCase)
        || value.Equals("dashboard", StringComparison.OrdinalIgnoreCase)
        || value.Equals("scan", StringComparison.OrdinalIgnoreCase)
        || value.Equals("redeem", StringComparison.OrdinalIgnoreCase)
        || value.Equals("customers", StringComparison.OrdinalIgnoreCase)
        || value.Equals("redemptions", StringComparison.OrdinalIgnoreCase)
        || value.Equals("rewards", StringComparison.OrdinalIgnoreCase)
        || value.Equals("campaigns", StringComparison.OrdinalIgnoreCase)
        || value.Equals("notifications", StringComparison.OrdinalIgnoreCase)
        || value.Equals("marketing-notifications", StringComparison.OrdinalIgnoreCase)
        || value.Equals("config", StringComparison.OrdinalIgnoreCase)
        || value.Equals("logout", StringComparison.OrdinalIgnoreCase)
        || value.Equals("css", StringComparison.OrdinalIgnoreCase)
        || value.Equals("js", StringComparison.OrdinalIgnoreCase);
}
