using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Provisioning;

public sealed record ProvisionTenantCommand(
    string Slug,
    string DisplayName,
    string? TimeZoneId,
    string AdminUsername,
    string AdminPassword,
    string? PrimaryColor = null,
    string? SecondaryColor = null,
    string? SupportPhone = null,
    string? WhatsAppUrl = null,
    string? InstagramUrl = null,
    string? TermsUrl = null) : IRequest<Result<ProvisionTenantResult>>;

public sealed record ProvisionTenantResult(
    Guid TenantId,
    string TenantSlug,
    Guid AdminUserId,
    string SubscriptionStatus);
