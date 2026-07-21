using LoyaltyCloud.Application.Config.Commands.UpdateProgramConfig;
using LoyaltyCloud.Application.Config.Queries.GetProgramConfig;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[Route("api/config")]
[Produces("application/json")]
public sealed class ConfigController : ControllerBase
{
    private readonly ISender _sender;

    public ConfigController(ISender sender) => _sender = sender;

    /// <summary>GET /api/config — todas las reglas vigentes.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _sender.Send(new GetProgramConfigQuery(), ct);
        return Ok(result.Value);
    }

    /// <summary>PUT /api/config — actualiza una o más reglas. Auditado.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateConfigRequest body,
        [FromHeader(Name = "X-Operator-Id")] string? operatorId,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new UpdateProgramConfigCommand(body.Entries, operatorId ?? "api"),
            ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Configuración", Detail = result.Error });

        return NoContent();
    }

    public sealed record UpdateConfigRequest(IReadOnlyList<ConfigEntry> Entries);
}
