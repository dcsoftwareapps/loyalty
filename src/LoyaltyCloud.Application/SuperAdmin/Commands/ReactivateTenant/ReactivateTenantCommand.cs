using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.ReactivateTenant;

public sealed record ReactivateTenantCommand(Guid TenantId) : IRequest<Result>;
