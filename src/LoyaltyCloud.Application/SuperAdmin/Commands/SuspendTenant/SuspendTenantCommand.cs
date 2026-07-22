using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.SuspendTenant;

public sealed record SuspendTenantCommand(Guid TenantId) : IRequest<Result>;
