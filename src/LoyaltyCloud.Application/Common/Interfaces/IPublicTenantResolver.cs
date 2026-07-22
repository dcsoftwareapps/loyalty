namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IPublicTenantResolver
{
    Task<PublicTenantInfo?> ResolveBySlugAsync(
        string tenantSlug,
        CancellationToken cancellationToken = default);
}

public sealed record PublicTenantInfo(
    Guid TenantId,
    string Slug,
    string DisplayName,
    bool IsActive,
    string? SubscriptionStatus,
    bool IsOperational,
    string PrimaryColor,
    string SecondaryColor,
    string? LogoUrl,
    string? SupportPhone,
    string? WhatsAppUrl,
    string? InstagramUrl,
    string? TermsUrl);

