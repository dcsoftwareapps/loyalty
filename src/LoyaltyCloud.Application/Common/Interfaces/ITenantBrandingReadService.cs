namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ITenantBrandingReadService
{
    Task<TenantBrandingInfo> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public sealed record TenantBrandingInfo(
    Guid TenantId,
    string TenantSlug,
    string DisplayName,
    string PrimaryColor,
    string SecondaryColor,
    string? LogoUrl,
    string? SupportPhone,
    string? WhatsAppUrl,
    string? InstagramUrl,
    string? TermsUrl);
