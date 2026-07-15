using KBeauty.Loyalty.Application.Campaigns;
using KBeauty.Loyalty.Application.Campaigns.Commands.ActivatePointCampaign;
using KBeauty.Loyalty.Application.Campaigns.Commands.CreatePointCampaign;
using KBeauty.Loyalty.Application.Campaigns.Commands.DeactivatePointCampaign;
using KBeauty.Loyalty.Application.Campaigns.Commands.UpdatePointCampaign;
using KBeauty.Loyalty.Application.Campaigns.Queries.GetPointCampaignById;
using KBeauty.Loyalty.Application.Campaigns.Queries.ListPointCampaigns;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace KBeauty.Loyalty.API.Controllers;

[ApiController]
[Route("api/campaigns")]
[Produces("application/json")]
public sealed class CampaignsController : ControllerBase
{
    private readonly ISender _sender;

    public CampaignsController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PointCampaignAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _sender.Send(new ListPointCampaignsQuery(), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Campanas", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PointCampaignAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetPointCampaignByIdQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Campana no encontrada", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PointCampaignAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CampaignRequest body, CancellationToken ct)
    {
        var result = await _sender.Send(
            new CreatePointCampaignCommand(
                body.Name,
                body.Description,
                body.Multiplier,
                body.MinimumPurchaseAmount,
                body.LevelEligibility,
                body.StartsAtUtc,
                body.EndsAtUtc,
                body.IsActive),
            ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Crear campana", Detail = result.Error });

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PointCampaignAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CampaignRequest body, CancellationToken ct)
    {
        var result = await _sender.Send(
            new UpdatePointCampaignCommand(
                id,
                body.Name,
                body.Description,
                body.Multiplier,
                body.MinimumPurchaseAmount,
                body.LevelEligibility,
                body.StartsAtUtc,
                body.EndsAtUtc,
                body.IsActive),
            ct);

        if (result.IsFailure)
            return CampaignProblem("Editar campana", result.Error);

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/activate")]
    [ProducesResponseType(typeof(PointCampaignAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new ActivatePointCampaignCommand(id), ct);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Activar campana", Detail = result.Error });

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(PointCampaignAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new DeactivatePointCampaignCommand(id), ct);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Desactivar campana", Detail = result.Error });

        return Ok(result.Value);
    }

    private IActionResult CampaignProblem(string title, string error)
    {
        if (error.StartsWith("No se encontro", StringComparison.Ordinal))
            return NotFound(new ProblemDetails { Title = title, Detail = error });

        return BadRequest(new ProblemDetails { Title = title, Detail = error });
    }

    public sealed record CampaignRequest(
        string Name,
        string Description,
        int Multiplier,
        decimal? MinimumPurchaseAmount,
        CampaignLevelEligibility LevelEligibility,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        bool IsActive = true);
}
