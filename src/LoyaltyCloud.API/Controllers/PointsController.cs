using LoyaltyCloud.Application.Points.Commands.AddPoints;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.API.Controllers;

/// <summary>Suma de puntos por compra — operación principal en tienda.</summary>
[ApiController]
[Route("api/points")]
[Produces("application/json")]
public sealed class PointsController : ControllerBase
{
    private readonly ISender _sender;

    public PointsController(ISender sender) => _sender = sender;

    /// <summary>POST /api/points — acredita puntos a una tarjeta.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AddPointsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Post(
        [FromBody] AddPointsRequest body,
        [FromHeader(Name = "X-Operator-Id")] string? operatorId,
        CancellationToken ct)
    {
        var command = new AddPointsCommand(
            SerialNumber: body.SerialNumber,
            PurchaseAmount: body.PurchaseAmount,
            OperatorId: operatorId ?? "api");

        var result = await _sender.Send(command, ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Validación", Detail = result.Error });

        return Ok(result.Value);
    }

    /// <summary>Body del POST — separado del Command para no exponer OperatorId al cliente.</summary>
    public sealed record AddPointsRequest(string SerialNumber, decimal PurchaseAmount);
}
