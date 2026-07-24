using LoyaltyCloud.Application.Levels;
using LoyaltyCloud.Application.Levels.Commands.UpdateTenantLoyaltyLevels;
using LoyaltyCloud.Application.Levels.Queries.ListTenantLoyaltyLevels;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[Route("api/levels")]
[Produces("application/json")]
public sealed class LevelsController : ControllerBase
{
    private readonly ISender _sender;

    public LevelsController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TenantLoyaltyLevelAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _sender.Send(new ListTenantLoyaltyLevelsQuery(), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Niveles", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpPut]
    [ProducesResponseType(typeof(UpdateTenantLoyaltyLevelsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateTenantLoyaltyLevelsRequest body, CancellationToken ct)
    {
        var operatorId = Request.Headers["X-Operator-Id"].ToString();
        var result = await _sender.Send(new UpdateTenantLoyaltyLevelsCommand(
            body.Levels,
            string.IsNullOrWhiteSpace(operatorId) ? "api-admin" : operatorId),
            ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Actualizar niveles", Detail = result.Error });

        return Ok(result.Value);
    }

    public sealed record UpdateTenantLoyaltyLevelsRequest(
        IReadOnlyList<TenantLoyaltyLevelUpdateItemDto> Levels);
}
