using System.Text;
using LoyaltyCloud.Infrastructure.Configuration;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed class GoogleWalletIdGenerator
{
    public string BuildClassId(GoogleWalletOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var issuerId = RequireIssuerId(options);
        var suffix = NormalizeSuffix(options.ClassSuffix, "ClassSuffix");
        return $"{issuerId}.{suffix}";
    }

    public string BuildObjectId(GoogleWalletOptions options, Guid tenantId, string serialNumber)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new ArgumentException("Serial requerido.", nameof(serialNumber));

        var issuerId = RequireIssuerId(options);
        var prefix = NormalizeSuffix(options.ObjectIdPrefix, "ObjectIdPrefix");
        var tenant = NormalizeSuffix(tenantId.ToString("N")[..12], nameof(tenantId));
        var serial = NormalizeSuffix(serialNumber, nameof(serialNumber));
        return $"{issuerId}.{prefix}-{tenant}-{serial}";
    }

    private static string RequireIssuerId(GoogleWalletOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IssuerId))
            throw new InvalidOperationException("GoogleWallet:IssuerId es requerido.");

        return NormalizeSuffix(options.IssuerId, "IssuerId");
    }

    private static string NormalizeSuffix(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"GoogleWallet:{name} es requerido.");

        var builder = new StringBuilder(value.Trim().Length);
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (ch is '-' or '_' or '.')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('_');
            }
        }

        var normalized = builder.ToString().Trim('.', '-', '_');
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException($"GoogleWallet:{name} no produce un identificador valido.");

        return normalized;
    }
}

