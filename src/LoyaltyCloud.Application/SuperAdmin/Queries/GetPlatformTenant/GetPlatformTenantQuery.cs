using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Queries.GetPlatformTenant;

public sealed record GetPlatformTenantQuery(Guid TenantId) : IRequest<PlatformTenantDetailDto?>;
