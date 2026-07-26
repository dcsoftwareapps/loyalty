using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.RemoveTenantLogo;

internal sealed class RemoveTenantLogoHandler : IRequestHandler<RemoveTenantLogoCommand, Result>
{
    private readonly ITenantBrandingLogoService _logos;

    public RemoveTenantLogoHandler(ITenantBrandingLogoService logos)
    {
        _logos = logos;
    }

    public Task<Result> Handle(RemoveTenantLogoCommand request, CancellationToken cancellationToken) =>
        _logos.RemoveAsync(request.TenantId, cancellationToken);
}
