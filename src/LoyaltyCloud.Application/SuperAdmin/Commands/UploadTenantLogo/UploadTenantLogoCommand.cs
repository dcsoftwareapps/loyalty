using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.UploadTenantLogo;

public sealed record UploadTenantLogoCommand(
    Guid TenantId,
    string FileName,
    string ContentType,
    Stream Content,
    long ContentLength) : IRequest<Result<TenantBrandingLogoResult>>;
