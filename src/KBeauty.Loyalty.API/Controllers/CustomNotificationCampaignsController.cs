using KBeauty.Loyalty.Application.Notifications.Custom;
using KBeauty.Loyalty.Application.Notifications.Custom.Commands.CancelCustomNotificationCampaign;
using KBeauty.Loyalty.Application.Notifications.Custom.Commands.CreateCustomNotificationCampaign;
using KBeauty.Loyalty.Application.Notifications.Custom.Commands.SendCustomNotificationCampaign;
using KBeauty.Loyalty.Application.Notifications.Custom.Queries.GetCustomNotificationCampaignById;
using KBeauty.Loyalty.Application.Notifications.Custom.Queries.ListCustomNotificationCampaigns;
using KBeauty.Loyalty.Application.Notifications.Custom.Queries.PreviewCustomNotificationAudience;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace KBeauty.Loyalty.API.Controllers;

[ApiController]
[Route("api/custom-notification-campaigns")]
[Produces("application/json")]
public sealed class CustomNotificationCampaignsController : ControllerBase
{
    private readonly ISender _sender;

    public CustomNotificationCampaignsController(ISender sender) => _sender = sender;

    [HttpPost("preview")]
    [ProducesResponseType(typeof(CustomNotificationAudiencePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview([FromBody] PreviewCustomNotificationAudienceRequest body, CancellationToken ct)
    {
        var result = await _sender.Send(new PreviewCustomNotificationAudienceQuery(
            body.AudienceType,
            body.MinimumPoints,
            body.PointsExpiringDaysAhead,
            body.SampleSize ?? 25), ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Preview de audiencia", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomNotificationCampaignDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] CustomNotificationCampaignStatus? status = null,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new ListCustomNotificationCampaignsQuery(status, take), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Campanas personalizadas", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomNotificationCampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetCustomNotificationCampaignByIdQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Campana personalizada no encontrada", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomNotificationCampaignDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CustomNotificationCampaignRequest body, CancellationToken ct)
    {
        var result = await _sender.Send(new CreateCustomNotificationCampaignCommand(
            body.Name,
            body.Title,
            body.ShortMessage,
            body.LongMessage,
            body.AudienceType,
            body.MinimumPoints,
            body.PointsExpiringDaysAhead,
            body.ScheduledAtUtc,
            body.DisplayUntilUtc,
            body.SendImmediately), ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Crear campana personalizada", Detail = result.Error });

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPost("{id:guid}/send")]
    [ProducesResponseType(typeof(CustomNotificationCampaignProcessingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new SendCustomNotificationCampaignCommand(id), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Enviar campana personalizada", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/cancel")]
    [ProducesResponseType(typeof(CustomNotificationCampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new CancelCustomNotificationCampaignCommand(id), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Cancelar campana personalizada", Detail = result.Error });

        return Ok(result.Value);
    }

    public sealed record PreviewCustomNotificationAudienceRequest(
        CustomNotificationAudienceType AudienceType,
        int? MinimumPoints,
        int? PointsExpiringDaysAhead,
        int? SampleSize);

    public sealed record CustomNotificationCampaignRequest(
        string Name,
        string Title,
        string ShortMessage,
        string LongMessage,
        CustomNotificationAudienceType AudienceType,
        int? MinimumPoints,
        int? PointsExpiringDaysAhead,
        DateTime? ScheduledAtUtc,
        DateTime DisplayUntilUtc,
        bool SendImmediately);
}
