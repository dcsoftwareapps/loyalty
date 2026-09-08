using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.API.Auth;

public sealed class CashierAccessTokenService
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);
    private readonly CashierAuthOptions _options;
    private readonly IDateTimeProvider _clock;

    public CashierAccessTokenService(
        IOptions<CashierAuthOptions> options,
        IDateTimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public CashierTokenResult CreateToken(Tenant tenant, TenantAdminUser user)
    {
        var now = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
        var lifetime = TimeSpan.FromMinutes(Math.Max(1, _options.AccessTokenMinutes));
        var expiresAt = now.Add(lifetime);
        var payload = new CashierTokenPayload(
            _options.Issuer,
            _options.Audience,
            user.Id.ToString(),
            tenant.Id.ToString(),
            tenant.Slug,
            user.Role.ToString(),
            user.Username,
            now.ToUnixTimeSeconds(),
            expiresAt.ToUnixTimeSeconds());

        var payloadJson = JsonSerializer.SerializeToUtf8Bytes(payload, CashierTokenJsonContext.Default.CashierTokenPayload);
        var encodedPayload = WebEncoders.Base64UrlEncode(payloadJson);
        var signature = Sign(encodedPayload);
        return new CashierTokenResult($"v1.{encodedPayload}.{signature}", expiresAt);
    }

    public CashierTokenValidationResult ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return CashierTokenValidationResult.Fail("missing_token");

        var parts = token.Split('.');
        if (parts.Length != 3 || !string.Equals(parts[0], "v1", StringComparison.Ordinal))
            return CashierTokenValidationResult.Fail("invalid_format");

        var expectedSignature = Sign(parts[1]);
        var providedSignature = parts[2];
        if (!FixedTimeEquals(expectedSignature, providedSignature))
            return CashierTokenValidationResult.Fail("invalid_signature");

        CashierTokenPayload? payload;
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(parts[1]);
            payload = JsonSerializer.Deserialize(bytes, CashierTokenJsonContext.Default.CashierTokenPayload);
        }
        catch (Exception)
        {
            return CashierTokenValidationResult.Fail("invalid_payload");
        }

        if (payload is null
            || !string.Equals(payload.Issuer, _options.Issuer, StringComparison.Ordinal)
            || !string.Equals(payload.Audience, _options.Audience, StringComparison.Ordinal)
            || !CashierAuthDefaults.AllowedRoles.Contains(payload.Role, StringComparer.Ordinal))
        {
            return CashierTokenValidationResult.Fail("invalid_claims");
        }

        var now = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt);
        if (expiresAt <= now.Subtract(ClockSkew))
            return CashierTokenValidationResult.Fail("expired_token");

        if (!Guid.TryParse(payload.Subject, out _)
            || !Guid.TryParse(payload.TenantId, out _)
            || string.IsNullOrWhiteSpace(payload.TenantSlug)
            || string.IsNullOrWhiteSpace(payload.Name))
        {
            return CashierTokenValidationResult.Fail("invalid_claims");
        }

        var claims = new List<Claim>
        {
            new(CashierClaimTypes.Subject, payload.Subject),
            new(ClaimTypes.NameIdentifier, payload.Subject),
            new(CashierClaimTypes.TenantId, payload.TenantId),
            new(CashierClaimTypes.TenantSlug, payload.TenantSlug),
            new(CashierClaimTypes.Name, payload.Name),
            new(ClaimTypes.Name, payload.Name),
            new(ClaimTypes.Role, payload.Role),
            new(CashierClaimTypes.AuthTime, payload.IssuedAt.ToString(CultureInfo.InvariantCulture)),
            new(CashierClaimTypes.Issuer, payload.Issuer),
            new(CashierClaimTypes.Audience, payload.Audience),
            new(CashierClaimTypes.IssuedAt, payload.IssuedAt.ToString(CultureInfo.InvariantCulture)),
            new(CashierClaimTypes.ExpiresAt, payload.ExpiresAt.ToString(CultureInfo.InvariantCulture))
        };
        var identity = new ClaimsIdentity(
            claims,
            CashierAuthDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        return CashierTokenValidationResult.Success(new ClaimsPrincipal(identity), payload);
    }

    private string Sign(string encodedPayload)
    {
        var key = _options.SigningKey;
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("CashierAuth:SigningKey must be configured with at least 32 bytes.");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return WebEncoders.Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(encodedPayload)));
    }

    private static bool FixedTimeEquals(string expected, string provided)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                WebEncoders.Base64UrlDecode(expected),
                WebEncoders.Base64UrlDecode(provided));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record CashierTokenResult(string AccessToken, DateTimeOffset ExpiresAtUtc);

public sealed record CashierTokenValidationResult(
    bool Succeeded,
    ClaimsPrincipal? Principal,
    CashierTokenPayload? Payload,
    string? FailureReason)
{
    public static CashierTokenValidationResult Success(ClaimsPrincipal principal, CashierTokenPayload payload) =>
        new(true, principal, payload, null);

    public static CashierTokenValidationResult Fail(string reason) =>
        new(false, null, null, reason);
}

public sealed record CashierTokenPayload(
    string Issuer,
    string Audience,
    string Subject,
    string TenantId,
    string TenantSlug,
    string Role,
    string Name,
    long IssuedAt,
    long ExpiresAt);

[JsonSerializable(typeof(CashierTokenPayload))]
internal partial class CashierTokenJsonContext : JsonSerializerContext;
