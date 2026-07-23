using System.Security.Cryptography;
using System.Text;

namespace LoyaltyCloud.Common.Security;

public static class AdminApiSignature
{
    public const string TenantSlugHeader = "X-LoyaltyCloud-Admin-Tenant-Slug";
    public const string OperatorHeader = "X-LoyaltyCloud-Admin-Operator";
    public const string TimestampHeader = "X-LoyaltyCloud-Admin-Timestamp";
    public const string SignatureHeader = "X-LoyaltyCloud-Admin-Signature";

    public static string CreateSignature(
        string sharedSecret,
        string method,
        string pathAndQuery,
        string timestamp,
        string tenantSlug,
        string operatorId,
        byte[] body)
    {
        if (string.IsNullOrWhiteSpace(sharedSecret))
            throw new InvalidOperationException("Falta AdminApi:SharedSecret.");

        var canonical = BuildCanonicalRequest(
            method,
            pathAndQuery,
            timestamp,
            tenantSlug,
            operatorId,
            Sha256Hex(body));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sharedSecret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
    }

    public static bool VerifySignature(
        string sharedSecret,
        string method,
        string pathAndQuery,
        string timestamp,
        string tenantSlug,
        string operatorId,
        byte[] body,
        string providedSignature)
    {
        if (string.IsNullOrWhiteSpace(sharedSecret) || string.IsNullOrWhiteSpace(providedSignature))
            return false;

        var expected = CreateSignature(
            sharedSecret,
            method,
            pathAndQuery,
            timestamp,
            tenantSlug,
            operatorId,
            body);

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(providedSignature.Trim());
        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static string BuildCanonicalRequest(
        string method,
        string pathAndQuery,
        string timestamp,
        string tenantSlug,
        string operatorId,
        string bodySha256) =>
        string.Join('\n',
            method.Trim().ToUpperInvariant(),
            pathAndQuery.Trim(),
            timestamp.Trim(),
            tenantSlug.Trim().ToLowerInvariant(),
            operatorId.Trim(),
            bodySha256);

    private static string Sha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
