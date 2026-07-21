using LoyaltyCloud.Application.Common.Interfaces;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantContext : IMutableTenantContext
{
    public Guid? TenantId { get; private set; }
    public string? TenantSlug { get; private set; }
    public bool HasTenant => TenantId.HasValue && !string.IsNullOrWhiteSpace(TenantSlug);

    public void SetTenant(Guid tenantId, string tenantSlug)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(tenantSlug))
            throw new ArgumentException("TenantSlug requerido.", nameof(tenantSlug));

        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();
        if (HasTenant)
        {
            if (TenantId == tenantId && string.Equals(TenantSlug, normalizedSlug, StringComparison.Ordinal))
                return;

            throw new InvalidOperationException(
                $"TenantContext ya fue establecido para {TenantSlug} ({TenantId}) y no puede cambiarse a {normalizedSlug} ({tenantId}) dentro del mismo scope.");
        }

        TenantId = tenantId;
        TenantSlug = normalizedSlug;
    }

    public void Clear()
    {
        TenantId = null;
        TenantSlug = null;
    }
}
