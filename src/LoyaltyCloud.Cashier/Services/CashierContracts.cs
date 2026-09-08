using System.Text.Json.Serialization;

namespace LoyaltyCloud.Cashier.Services;

public sealed record CashierLoginRequest(
    [property: JsonPropertyName("tenantSlug")] string TenantSlug,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

public sealed record CashierLoginResponse(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("tokenType")] string TokenType,
    [property: JsonPropertyName("expiresAtUtc")] DateTimeOffset ExpiresAtUtc,
    [property: JsonPropertyName("expiresInSeconds")] int ExpiresInSeconds,
    [property: JsonPropertyName("tenantSlug")] string TenantSlug,
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("role")] string Role);

public sealed record CashierSession(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    string TenantSlug,
    Guid UserId,
    string Username,
    string Role)
{
    public bool IsExpired(DateTimeOffset now) => ExpiresAtUtc <= now;
}

public sealed record CashierLoginResult(
    bool Succeeded,
    CashierSession? Session,
    string? ErrorMessage)
{
    public static CashierLoginResult Success(CashierSession session) => new(true, session, null);

    public static CashierLoginResult Failure(string errorMessage) => new(false, null, errorMessage);
}
