using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Queries.ListPlatformTenants;

public sealed record ListPlatformTenantsQuery : IRequest<IReadOnlyList<PlatformTenantListItemDto>>;
