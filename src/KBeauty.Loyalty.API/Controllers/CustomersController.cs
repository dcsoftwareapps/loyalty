using KBeauty.Loyalty.Application.Customers.Commands.RegisterCustomer;
using KBeauty.Loyalty.Application.Customers.Queries.GetCustomerBySerial;
using KBeauty.Loyalty.Application.Customers.Queries.GetCustomerTransactions;
using KBeauty.Loyalty.Common.Pagination;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace KBeauty.Loyalty.API.Controllers;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public sealed class CustomersController : ControllerBase
{
    private readonly ISender _sender;

    public CustomersController(ISender sender) => _sender = sender;

    /// <summary>POST /api/customers — registra una clienta nueva y genera su pase.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RegisterCustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Registro", Detail = result.Error });

        return CreatedAtAction(
            actionName: nameof(GetBySerial),
            routeValues: new { serialNumber = result.Value.SerialNumber },
            value: result.Value);
    }

    /// <summary>GET /api/customers/{serialNumber} — busca clienta por serial escaneado.</summary>
    [HttpGet("{serialNumber}")]
    [ProducesResponseType(typeof(CustomerDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySerial(string serialNumber, CancellationToken ct)
    {
        var result = await _sender.Send(new GetCustomerBySerialQuery(serialNumber), ct);

        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "No encontrada", Detail = result.Error });

        return Ok(result.Value);
    }

    /// <summary>GET /api/customers/{serialNumber}/transactions — historial paginado.</summary>
    [HttpGet("{serialNumber}/transactions")]
    public async Task<IActionResult> GetTransactions(
        string serialNumber,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var pagination = new PaginationParams { PageNumber = pageNumber, PageSize = pageSize };
        var result = await _sender.Send(new GetCustomerTransactionsQuery(serialNumber, pagination), ct);

        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "No encontrada", Detail = result.Error });

        return Ok(result.Value);
    }
}
