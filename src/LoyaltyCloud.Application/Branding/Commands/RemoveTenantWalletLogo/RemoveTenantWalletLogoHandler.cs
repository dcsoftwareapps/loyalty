using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Branding.Commands.RemoveTenantWalletLogo;

internal sealed class RemoveTenantWalletLogoHandler : IRequestHandler<RemoveTenantWalletLogoCommand, Result>
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenantBrandingLogoService _logos;
    private readonly ITenantWalletCardBrandingService _walletBranding;

    public RemoveTenantWalletLogoHandler(
        ITenantContext tenantContext,
        ITenantBrandingLogoService logos,
        ITenantWalletCardBrandingService walletBranding)
    {
        _tenantContext = tenantContext;
        _logos = logos;
        _walletBranding = walletBranding;
    }

    public async Task<Result> Handle(RemoveTenantWalletLogoCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var result = await _logos.RemoveWalletLogoAsync(tenantId, cancellationToken);
        if (result.IsSuccess)
            await _walletBranding.RefreshInstalledApplePassesBestEffortAsync(tenantId, cancellationToken);

        return result;
    }
}
