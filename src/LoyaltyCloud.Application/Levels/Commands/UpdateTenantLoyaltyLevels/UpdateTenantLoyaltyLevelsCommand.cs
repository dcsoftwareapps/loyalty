using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Levels.Commands.UpdateTenantLoyaltyLevels;

public sealed record UpdateTenantLoyaltyLevelsCommand(
    IReadOnlyList<TenantLoyaltyLevelUpdateItemDto> Levels,
    string OperatorId)
    : IRequest<Result<UpdateTenantLoyaltyLevelsResultDto>>;
