using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Levels.Commands.UpdateTenantLoyaltyLevels;

public sealed class UpdateTenantLoyaltyLevelsHandler
    : IRequestHandler<UpdateTenantLoyaltyLevelsCommand, Result<UpdateTenantLoyaltyLevelsResultDto>>
{
    private readonly ITenantLoyaltyLevelManagementService _levels;

    public UpdateTenantLoyaltyLevelsHandler(ITenantLoyaltyLevelManagementService levels) => _levels = levels;

    public Task<Result<UpdateTenantLoyaltyLevelsResultDto>> Handle(
        UpdateTenantLoyaltyLevelsCommand request,
        CancellationToken ct) =>
        _levels.UpdateAsync(request.Levels, request.OperatorId, ct);
}
