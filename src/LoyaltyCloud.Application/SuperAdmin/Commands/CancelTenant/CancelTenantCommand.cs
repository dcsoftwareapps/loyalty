using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.CancelTenant;

public sealed record CancelTenantCommand(Guid TenantId) : IRequest<Result>;
