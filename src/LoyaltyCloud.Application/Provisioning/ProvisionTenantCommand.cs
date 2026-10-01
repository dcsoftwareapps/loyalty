using LoyaltyCloud.Common.Results;
using MediatR;
using LoyaltyCloud.Domain.Enums;

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
    string? TermsUrl = null,
    LoyaltyProgramType ProgramType = LoyaltyProgramType.Points) : IRequest<Result<ProvisionTenantResult>>;

public sealed record ProvisionTenantResult(
    Guid TenantId,
    string TenantSlug,
    Guid AdminUserId,
    string SubscriptionStatus);
