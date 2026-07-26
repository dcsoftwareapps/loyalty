using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.DeleteTenant;

public sealed record DeleteTenantCommand(Guid TenantId, string ConfirmationSlug) : IRequest<Result>;
