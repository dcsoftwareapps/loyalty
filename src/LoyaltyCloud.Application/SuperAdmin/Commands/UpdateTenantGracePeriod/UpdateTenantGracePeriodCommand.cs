using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.UpdateTenantGracePeriod;

public sealed record UpdateTenantGracePeriodCommand(Guid TenantId, DateTime? NewGracePeriodEndUtc) : IRequest<Result>;
