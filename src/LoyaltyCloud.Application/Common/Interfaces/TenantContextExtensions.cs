namespace LoyaltyCloud.Application.Common.Interfaces;

public static class TenantContextExtensions
{
    public static Guid RequireTenantId(this ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        return tenantContext.TenantId
            ?? throw new InvalidOperationException(
                "No hay tenant resuelto para la operacion actual. " +
                "Establece IMutableTenantContext antes de ejecutar operaciones comerciales.");
    }
}
