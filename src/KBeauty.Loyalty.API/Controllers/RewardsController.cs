using KBeauty.Loyalty.Application.Rewards;
using KBeauty.Loyalty.Application.Rewards.Commands.ActivateReward;
using KBeauty.Loyalty.Application.Rewards.Commands.CreateReward;
using KBeauty.Loyalty.Application.Rewards.Commands.DeactivateReward;
using KBeauty.Loyalty.Application.Rewards.Commands.UpdateReward;
using KBeauty.Loyalty.Application.Rewards.Queries.GetRewardById;
using KBeauty.Loyalty.Application.Rewards.Queries.ListRewards;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace KBeauty.Loyalty.API.Controllers;

[ApiController]
[Route("api/rewards")]
[Produces("application/json")]
public sealed class RewardsController : ControllerBase
{
    private readonly ISender _sender;

    public RewardsController(ISender sender) => _sender = sender;

    /// <summary>GET /api/rewards - catalogo completo para administracion.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RewardAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] bool activeOnly = false,
        [FromQuery] bool includeExpired = true,
        [FromQuery] string? minLevel = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new ListRewardsQuery(activeOnly, includeExpired, minLevel), ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Recompensas", Detail = result.Error });

        return Ok(result.Value);
    }

    /// <summary>GET /api/rewards/{id} - detalle de una recompensa.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RewardAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetRewardByIdQuery(id), ct);

        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Recompensa no encontrada", Detail = result.Error });

        return Ok(result.Value);
    }

    /// <summary>POST /api/rewards - crea una recompensa.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RewardAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] RewardRequest body, CancellationToken ct)
    {
        var result = await _sender.Send(
            new CreateRewardCommand(
                body.Name,
                body.Description,
                body.PointsCost,
                body.MinLevel,
                body.IsMonthlyProduct,
                body.ValidFrom,
                body.ValidTo,
                body.IsActive),
            ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Crear recompensa", Detail = result.Error });

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>PUT /api/rewards/{id} - edita una recompensa existente.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RewardAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] RewardRequest body, CancellationToken ct)
    {
        var result = await _sender.Send(
            new UpdateRewardCommand(
                id,
                body.Name,
                body.Description,
                body.PointsCost,
                body.MinLevel,
                body.IsMonthlyProduct,
                body.ValidFrom,
                body.ValidTo,
                body.IsActive),
            ct);

        if (result.IsFailure)
            return RewardProblem("Editar recompensa", result.Error);

        return Ok(result.Value);
    }

    /// <summary>PUT /api/rewards/{id}/activate - activa una recompensa.</summary>
    [HttpPut("{id:guid}/activate")]
    [ProducesResponseType(typeof(RewardAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new ActivateRewardCommand(id), ct);

        if (result.IsFailure)
            return RewardProblem("Activar recompensa", result.Error);

        return Ok(result.Value);
    }

    /// <summary>PUT /api/rewards/{id}/deactivate - desactiva una recompensa.</summary>
    [HttpPut("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(RewardAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new DeactivateRewardCommand(id), ct);

        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Desactivar recompensa", Detail = result.Error });

        return Ok(result.Value);
    }

    private IActionResult RewardProblem(string title, string error)
    {
        if (error.StartsWith("No se encontro", StringComparison.Ordinal))
            return NotFound(new ProblemDetails { Title = title, Detail = error });

        return BadRequest(new ProblemDetails { Title = title, Detail = error });
    }

    public sealed record RewardRequest(
        string Name,
        string Description,
        int PointsCost,
        string MinLevel,
        bool IsMonthlyProduct,
        DateTime? ValidFrom,
        DateTime? ValidTo,
        bool IsActive = true);
}
