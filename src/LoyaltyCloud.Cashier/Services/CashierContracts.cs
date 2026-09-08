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

public sealed record CashierCustomerDetail(
    [property: JsonPropertyName("customerId")] Guid CustomerId,
    [property: JsonPropertyName("fullName")] string FullName,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("serialNumber")] string SerialNumber,
    [property: JsonPropertyName("currentPoints")] int CurrentPoints,
    [property: JsonPropertyName("lifetimePoints")] int LifetimePoints,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("pointsToNextLevel")] int PointsToNextLevel,
    [property: JsonPropertyName("isActive")] bool IsActive);

public sealed record CashierAddPointsRequest(
    [property: JsonPropertyName("serialNumber")] string SerialNumber,
    [property: JsonPropertyName("purchaseAmount")] decimal PurchaseAmount);

public sealed record CashierAddPointsResponse(
    [property: JsonPropertyName("pointsAdded")] int PointsAdded,
    [property: JsonPropertyName("newTotal")] int NewTotal,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("leveledUp")] bool LeveledUp,
    [property: JsonPropertyName("birthdayBonusApplied")] bool BirthdayBonusApplied,
    [property: JsonPropertyName("basePoints")] int BasePoints,
    [property: JsonPropertyName("campaignBonusPoints")] int CampaignBonusPoints,
    [property: JsonPropertyName("appliedMultiplier")] decimal AppliedMultiplier,
    [property: JsonPropertyName("campaignId")] Guid? CampaignId,
    [property: JsonPropertyName("campaignName")] string? CampaignName);

public sealed record CashierOperationResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorMessage,
    bool Unauthorized = false)
{
    public static CashierOperationResult<T> Success(T value) => new(true, value, null);

    public static CashierOperationResult<T> Failure(string errorMessage) => new(false, default, errorMessage);

    public static CashierOperationResult<T> SessionExpired() =>
        new(false, default, "Tu sesión expiró. Inicia sesión de nuevo.", Unauthorized: true);
}
