using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.UploadTenantLogo;

internal sealed class UploadTenantLogoHandler
    : IRequestHandler<UploadTenantLogoCommand, Result<TenantBrandingLogoResult>>
{
    private readonly ITenantBrandingLogoService _logos;

    public UploadTenantLogoHandler(ITenantBrandingLogoService logos)
    {
        _logos = logos;
    }

    public Task<Result<TenantBrandingLogoResult>> Handle(
        UploadTenantLogoCommand request,
        CancellationToken cancellationToken) =>
        _logos.UploadAsync(
            request.TenantId,
            request.FileName,
            request.ContentType,
            request.Content,
            request.ContentLength,
            cancellationToken);
}
