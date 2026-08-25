using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Branding.Commands.UploadTenantWalletLogo;

public sealed record UploadTenantWalletLogoCommand(
    string FileName,
    string ContentType,
    Stream Content,
    long ContentLength) : IRequest<Result<TenantBrandingLogoResult>>;
