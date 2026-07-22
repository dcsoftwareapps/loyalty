using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.ExtendTenantTrial;

public sealed record ExtendTenantTrialCommand(Guid TenantId, DateTime NewTrialEndUtc) : IRequest<Result>;
