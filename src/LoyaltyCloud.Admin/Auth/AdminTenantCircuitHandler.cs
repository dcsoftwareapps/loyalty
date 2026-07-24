using Microsoft.AspNetCore.Components.Server.Circuits;

namespace LoyaltyCloud.Admin.Auth;

public sealed class AdminTenantCircuitHandler : CircuitHandler
{
    private readonly AdminTenantContextInitializer _initializer;

    public AdminTenantCircuitHandler(AdminTenantContextInitializer initializer)
    {
        _initializer = initializer;
    }

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        await _initializer.EnsureTenantContextAsync(cancellationToken);
    }

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        await _initializer.EnsureTenantContextAsync(cancellationToken);
    }
}
