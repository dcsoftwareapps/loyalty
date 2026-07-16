using KBeauty.Loyalty.Application.Admin.Queries.GetAdminDashboard;
using KBeauty.Loyalty.Application.Levels.Commands.RecalculateLevels;
using KBeauty.Loyalty.Application.Notifications.BirthdayBenefit;
using KBeauty.Loyalty.Application.Notifications.MonthlyProduct;
using KBeauty.Loyalty.Application.Notifications.PointCampaign;
using KBeauty.Loyalty.Application.Notifications.PointsExpiration;
using KBeauty.Loyalty.Application.Notifications.Queries.ListBirthdayBenefitNotificationCandidates;
using KBeauty.Loyalty.Application.Notifications.Queries.ListMonthlyProductNotificationCandidates;
using KBeauty.Loyalty.Application.Notifications.Queries.ListPointCampaignNotificationCandidates;
using KBeauty.Loyalty.Application.Notifications.Queries.ListPointExpirationNotificationCandidates;
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

    /// <summary>POST /api/admin/levels/recalculate - recalcula niveles con ventana movil de 12 meses.</summary>
    [HttpPost("levels/recalculate")]
    [ProducesResponseType(typeof(RecalculateLevelsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecalculateLevels(
        [FromHeader(Name = "X-Operator-Id")] string? operatorId,
        CancellationToken ct)
    {
        var result = await _sender.Send(new RecalculateLevelsCommand(operatorId ?? "api-admin"), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Niveles", Detail = result.Error });

        return Ok(result.Value);
    }

    /// <summary>GET /api/admin/points/expiration-notification-candidates - vista previa de avisos por expirar.</summary>
    [HttpGet("points/expiration-notification-candidates")]
    [ProducesResponseType(typeof(IReadOnlyList<PointsExpirationNotificationCandidateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPointExpirationNotificationCandidates(
        [FromQuery] int daysAhead = 15,
        [FromQuery] string timeZoneId = "America/Tijuana",
        [FromQuery] bool includeAlreadyNotified = false,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new ListPointExpirationNotificationCandidatesQuery(daysAhead, timeZoneId, includeAlreadyNotified),
            ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Avisos de expiracion", Detail = result.Error });

        return Ok(result.Value);
    }

    /// <summary>GET /api/admin/rewards/monthly-product-notification-candidates - vista previa de avisos de Producto del mes.</summary>
    [HttpGet("rewards/monthly-product-notification-candidates")]
    [ProducesResponseType(typeof(MonthlyProductNotificationPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMonthlyProductNotificationCandidates(
        [FromQuery] string timeZoneId = "America/Tijuana",
        [FromQuery] bool includeAlreadyNotified = false,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new ListMonthlyProductNotificationCandidatesQuery(timeZoneId, includeAlreadyNotified),
            ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Avisos de Producto del mes", Detail = result.Error });

        return Ok(result.Value);
    }

    /// <summary>GET /api/admin/customers/birthday-notification-candidates - vista previa de avisos de cumpleanos.</summary>
    [HttpGet("customers/birthday-notification-candidates")]
    [ProducesResponseType(typeof(BirthdayBenefitNotificationPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBirthdayNotificationCandidates(
        [FromQuery] string timeZoneId = "America/Tijuana",
        [FromQuery] bool includeAlreadyNotified = false,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new ListBirthdayBenefitNotificationCandidatesQuery(timeZoneId, includeAlreadyNotified),
            ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Avisos de cumpleanos", Detail = result.Error });

        return Ok(result.Value);
    }

    /// <summary>GET /api/admin/campaigns/notification-candidates - vista previa de avisos de campanas de puntos.</summary>
    [HttpGet("campaigns/notification-candidates")]
    [ProducesResponseType(typeof(PointCampaignNotificationPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPointCampaignNotificationCandidates(
        [FromQuery] string timeZoneId = "America/Tijuana",
        [FromQuery] bool includeAlreadyNotified = false,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new ListPointCampaignNotificationCandidatesQuery(timeZoneId, includeAlreadyNotified),
            ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Avisos de campanas", Detail = result.Error });

        return Ok(result.Value);
    }
}
