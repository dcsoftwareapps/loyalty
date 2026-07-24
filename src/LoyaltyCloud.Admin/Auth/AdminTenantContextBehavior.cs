using MediatR;

namespace LoyaltyCloud.Admin.Auth;

/// <summary>
/// Restores the tenant context inside Blazor Server circuit scopes before
/// Admin-triggered MediatR requests reach tenant-owned Application handlers.
/// </summary>
public sealed class AdminTenantContextBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly AdminTenantContextInitializer _initializer;

    public AdminTenantContextBehavior(
        AdminTenantContextInitializer initializer)
    {
        _initializer = initializer;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        await _initializer.EnsureTenantContextAsync(cancellationToken);
        return await next();
    }
}
