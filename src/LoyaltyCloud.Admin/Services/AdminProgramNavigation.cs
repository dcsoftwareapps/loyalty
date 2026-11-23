using LoyaltyCloud.Domain.Enums;
using Microsoft.AspNetCore.WebUtilities;

namespace LoyaltyCloud.Admin.Services;

public static class AdminProgramNavigation
{
    public static bool IsAllowed(LoyaltyProgramType program, string pathAndQuery)
    {
        var parts = pathAndQuery.Split('?', 2);
        var path = "/" + parts[0].Trim('/').ToLowerInvariant();
        if (path == "/stamps") return program == LoyaltyProgramType.Stamps;
        if (program == LoyaltyProgramType.Points) return true;
        if (path is "/dashboard" or "/scan" or "/redeem" or "/redemptions" or "/rewards" or "/levels" or "/campaigns")
            return false;
        if (path == "/reports" || path.StartsWith("/reports/", StringComparison.Ordinal)) return false;
        return path != "/config" || parts.Length < 2
            || !string.Equals(QueryHelpers.ParseQuery(parts[1])["section"], "points", StringComparison.OrdinalIgnoreCase);
    }
}
