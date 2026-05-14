using KBeauty.Loyalty.Application.Redemptions.Commands.ConfirmRedemption;
using KBeauty.Loyalty.Application.Redemptions.Commands.RedeemReward;
using KBeauty.Loyalty.Application.Redemptions.Queries.GetRedemptionCatalog;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace KBeauty.Loyalty.API.Controllers;

[ApiController]
[Route("api/redemptions")]
[Produces("application/json")]
public sealed class RedemptionsController : ControllerBase
{
    private readonly ISender _sender;

    public RedemptionsController(ISender sender) => _sender = sender;

    /// <summary>POST /api/redemptions — la clienta inicia un canje.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RedemptionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Redeem(
        [FromBody] RedeemRewardRequest body,
        [FromHeader(Name = "X-Operator-Id")] string? operatorId,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new RedeemRewardCommand(body.SerialNumber, body.RewardCatalogItemId, operatorId ?? "api"),
            ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Canje", Detail = result.Error });

        return CreatedAtAction(
            actionName: nameof(Confirm),
            routeValues: new { id = result.Value.RedemptionId },
            value: result.Value);
    }

    /// <summary>PUT /api/redemptions/{id}/confirm — el operador confirma entrega.</summary>
    [HttpPut("{id:guid}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm(
        Guid id,
        [FromBody] ConfirmRedemptionRequest? body,
        [FromHeader(Name = "X-Operator-Id")] string? operatorId,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new ConfirmRedemptionCommand(id, operatorId ?? "api", body?.Notes),
            ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Confirmación", Detail = result.Error });

        return NoContent();
    }

    /// <summary>GET /api/redemptions/catalog/{serialNumber} — catálogo filtrado al nivel de la clienta.</summary>
    [HttpGet("catalog/{serialNumber}")]
    public async Task<IActionResult> GetCatalog(string serialNumber, CancellationToken ct)
    {
        var result = await _sender.Send(new GetRedemptionCatalogQuery(serialNumber), ct);

        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "No encontrada", Detail = result.Error });

        return Ok(result.Value);
    }

    public sealed record RedeemRewardRequest(string SerialNumber, Guid RewardCatalogItemId);
    public sealed record ConfirmRedemptionRequest(string? Notes);
}
