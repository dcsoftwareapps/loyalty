using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Branding.Commands.UpdateWalletCardBranding;

internal sealed class UpdateWalletCardBrandingHandler
    : IRequestHandler<UpdateWalletCardBrandingCommand, Result<TenantBrandingInfo>>
{
    private readonly ITenantWalletCardBrandingService _service;

    public UpdateWalletCardBrandingHandler(ITenantWalletCardBrandingService service)
    {
        _service = service;
    }

    public Task<Result<TenantBrandingInfo>> Handle(
        UpdateWalletCardBrandingCommand request,
        CancellationToken cancellationToken) =>
        _service.UpdateAsync(
            request.WalletBackgroundColor,
            request.WalletLogoScalePercent,
            cancellationToken);
}
