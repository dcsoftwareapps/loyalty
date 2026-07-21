namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ITenantWalletBrandingReadService
{
    Task<TenantWalletBrandingDto> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public sealed record TenantWalletBrandingDto(
    string DisplayName,
    string OrganizationName,
    string Description,
    string BackgroundColor,
    string ForegroundColor,
    string LabelColor,
    string ContactValue,
    string CustomerFallbackName,
    bool UsesBundledAssetsFallback,
    bool UsesLegacyContactFallback);

