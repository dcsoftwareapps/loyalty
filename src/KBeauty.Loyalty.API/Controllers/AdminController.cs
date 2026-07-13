using KBeauty.Loyalty.Application.Admin.Queries.GetAdminDashboard;
using KBeauty.Loyalty.Application.Points.Commands.ExpirePoints;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace KBeauty.Loyalty.API.Controllers;

[ApiController]
[Route("api/admin")]
[Produces("application/json")]
public sealed class AdminController : ControllerBase
{
    private readonly ISender _sender;

    public AdminController(ISender sender) => _sender = sender;

    /// <summary>GET /api/admin/dashboard — agregaciones para el panel principal.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var result = await _sender.Send(new GetAdminDashboardQuery(), ct);
        return Ok(result.Value);
    }

    /// <summary>POST /api/admin/points/expire — ejecuta expiracion FIFO de puntos vencidos.</summary>
    [HttpPost("points/expire")]
    [ProducesResponseType(typeof(ExpirePointsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExpirePoints(
        [FromHeader(Name = "X-Operator-Id")] string? operatorId,
        CancellationToken ct)
    {
        var result = await _sender.Send(new ExpirePointsCommand(operatorId ?? "api-admin"), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Validacion", Detail = result.Error });

        return Ok(result.Value);
    }
}
