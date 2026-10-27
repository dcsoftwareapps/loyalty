using System.Text;
using LoyaltyCloud.Infrastructure.Configuration;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed class GoogleWalletIdGenerator
{
    public string BuildClassId(GoogleWalletOptions options, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido.", nameof(tenantId));

        var issuerId = RequireIssuerId(options);
        var suffix = NormalizeSuffix(options.ClassSuffix, "ClassSuffix");
        return $"{issuerId}.{suffix}-{tenantId:N}";
    }

    public string BuildLegacyClassId(GoogleWalletOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return $"{RequireIssuerId(options)}.{NormalizeSuffix(options.ClassSuffix, "ClassSuffix")}";
    }

    public string BuildStampClassId(GoogleWalletOptions options, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId requerido.", nameof(tenantId));
        return $"{RequireIssuerId(options)}.{NormalizeSuffix(options.ClassSuffix, "ClassSuffix")}-stamps-{tenantId:N}";
    }

    public string BuildStampObjectId(GoogleWalletOptions options, Guid tenantId, string serialNumber)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId requerido.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(serialNumber)) throw new ArgumentException("Serial requerido.", nameof(serialNumber));
        var tenant = NormalizeSuffix(tenantId.ToString("N")[..12], nameof(tenantId));
        var serial = NormalizeSuffix(serialNumber, nameof(serialNumber));
        return $"{RequireIssuerId(options)}.{NormalizeSuffix(options.ObjectIdPrefix, "ObjectIdPrefix")}-stamps-{tenant}-{serial}";
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
            if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
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

