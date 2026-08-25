using LoyaltyCloud.Common.Results;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ITenantBrandingLogoService
{
    Task<Result<TenantBrandingLogoResult>> UploadAsync(
        Guid tenantId,
        string fileName,
        string contentType,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<Result> RemoveAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Result<TenantBrandingLogoResult>> UploadWalletLogoAsync(
        Guid tenantId,
        string fileName,
        string contentType,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<Result> RemoveWalletLogoAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record TenantBrandingLogoResult(
    Guid TenantId,
    string LogoBlobName,
    string? LogoUrl);
