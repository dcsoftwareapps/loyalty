using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Application.Provisioning;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ITenantProvisioningService
{
    Task<Result<ProvisionTenantResult>> ProvisionAsync(
        ProvisionTenantRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProvisionTenantRequest(
    string Slug,
    string DisplayName,
    string TimeZoneId,
    string AdminUsername,
    string AdminPassword,
    string? PrimaryColor,
    string? SecondaryColor,
    string? SupportPhone,
    string? WhatsAppUrl,
    string? InstagramUrl,
    string? TermsUrl);
