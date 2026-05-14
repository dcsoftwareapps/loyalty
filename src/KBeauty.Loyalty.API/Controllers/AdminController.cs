using KBeauty.Loyalty.Application.Admin.Queries.GetAdminDashboard;
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
}
