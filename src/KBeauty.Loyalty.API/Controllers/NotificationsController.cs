using KBeauty.Loyalty.Application.Notifications;
using KBeauty.Loyalty.Application.Notifications.Commands.CancelNotification;
using KBeauty.Loyalty.Application.Notifications.Commands.CreateNotification;
using KBeauty.Loyalty.Application.Notifications.Commands.ProcessNotification;
using KBeauty.Loyalty.Application.Notifications.Commands.RetryNotification;
using KBeauty.Loyalty.Application.Notifications.Queries.GetNotificationById;
using KBeauty.Loyalty.Application.Notifications.Queries.GetNotificationMetrics;
using KBeauty.Loyalty.Application.Notifications.Queries.ListNotifications;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace KBeauty.Loyalty.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? customerId = null,
        [FromQuery] NotificationType? type = null,
        [FromQuery] NotificationStatus? status = null,
        [FromQuery] NotificationChannel? channel = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new ListNotificationsQuery(customerId, type, status, channel, fromUtc, toUtc, take), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Notificaciones", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpGet("metrics")]
    [ProducesResponseType(typeof(NotificationMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Metrics(CancellationToken ct)
    {
        var result = await _sender.Send(new GetNotificationMetricsQuery(), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Metricas de notificaciones", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetNotificationByIdQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Notificacion no encontrada", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] NotificationRequest body, CancellationToken ct)
    {
        var result = await _sender.Send(new CreateNotificationCommand(
            body.SerialNumber,
            body.Type ?? NotificationType.Custom,
            body.Title,
            body.Message,
            body.ScheduledAtUtc,
            body.DisplayUntilUtc,
            body.Channels is { Count: > 0 } ? body.Channels : [NotificationChannel.AppleWallet],
            body.CorrelationId,
            body.Source ?? "api",
            body.MetadataJson,
            body.ProcessImmediately), ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Crear notificacion", Detail = result.Error });

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPost("{id:guid}/process")]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Process(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new ProcessNotificationCommand(id), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Procesar notificacion", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new RetryNotificationCommand(id), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Reintentar notificacion", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/cancel")]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new CancelNotificationCommand(id), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Cancelar notificacion", Detail = result.Error });

        return Ok(result.Value);
    }

    public sealed record NotificationRequest(
        string SerialNumber,
        string Title,
        string Message,
        NotificationType? Type,
        DateTime? ScheduledAtUtc,
        DateTime? DisplayUntilUtc,
        IReadOnlyList<NotificationChannel>? Channels,
        string? CorrelationId,
        string? Source,
        string? MetadataJson,
        bool ProcessImmediately = true);
}
