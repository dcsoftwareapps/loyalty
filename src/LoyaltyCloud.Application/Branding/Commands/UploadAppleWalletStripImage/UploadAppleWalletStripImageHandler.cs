using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Branding.Commands.UploadAppleWalletStripImage;

internal sealed class UploadAppleWalletStripImageHandler
    : IRequestHandler<UploadAppleWalletStripImageCommand, Result<TenantBrandingLogoResult>>
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenantBrandingLogoService _logos;
    private readonly ITenantWalletCardBrandingService _walletBranding;

    public UploadAppleWalletStripImageHandler(
        ITenantContext tenantContext,
        ITenantBrandingLogoService logos,
        ITenantWalletCardBrandingService walletBranding)
    {
        _tenantContext = tenantContext;
        _logos = logos;
        _walletBranding = walletBranding;
    }

    public async Task<Result<TenantBrandingLogoResult>> Handle(
        UploadAppleWalletStripImageCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var result = await _logos.UploadAppleWalletStripImageAsync(
            tenantId,
            request.FileName,
            request.ContentType,
            request.Content,
            request.ContentLength,
            cancellationToken);

        if (result.IsSuccess)
            await _walletBranding.RefreshInstalledApplePassesBestEffortAsync(tenantId, cancellationToken);

        return result;
    }
}
