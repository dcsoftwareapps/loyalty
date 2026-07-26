namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ITenantBrandingLogoUrlProvider
{
    string? GetDisplayUrl(string? logoBlobName);
}
