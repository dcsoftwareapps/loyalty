using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.RemoveTenantLogo;

public sealed record RemoveTenantLogoCommand(Guid TenantId) : IRequest<Result>;
