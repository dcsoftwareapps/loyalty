using LoyaltyCloud.Application.Customers.Commands.JoinCustomer;
using LoyaltyCloud.Application.Customers.Commands.UpdateCustomerBirthday;
using LoyaltyCloud.Infrastructure.Configuration;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[Route("api/public/join")]
[Produces("application/json")]
public sealed class PublicJoinController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IWebHostEnvironment _environment;
    private readonly ApplePassOptions _options;

    public PublicJoinController(
        ISender sender,
        IWebHostEnvironment environment,
        IOptions<ApplePassOptions> options)
    {
        _sender = sender;
        _environment = environment;
        _options = options.Value;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PublicJoinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Join([FromBody] PublicJoinRequest body, CancellationToken ct)
    {
        var result = await _sender.Send(new JoinCustomerCommand(
            body.FirstName,
            body.LastName,
            body.Phone), ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Alta KBeauty MX", Detail = result.Error });

        return Ok(ToResponse(result.Value));
    }

    [HttpPut("{serialNumber}/birthday")]
    [ProducesResponseType(typeof(PublicBirthdayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateBirthday(
        string serialNumber,
        [FromBody] PublicBirthdayRequest body,
        CancellationToken ct)
    {
        var result = await _sender.Send(new UpdateCustomerBirthdayCommand(
            CustomerId: null,
            SerialNumber: serialNumber,
            Day: body.Day,
            Month: body.Month), ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Cumpleanos", Detail = result.Error });

        return Ok(new PublicBirthdayResponse(
            result.Value.Day,
            result.Value.Month,
            "Cumpleanos guardado."));
    }

    private PublicJoinResponse ToResponse(JoinCustomerResponse value) =>
        new(
            value.CustomerId,
            value.SerialNumber,
            value.FullName,
            value.Phone,
            value.AlreadyExists,
            value.Message,
            BuildPassDownloadUrl(value.SerialNumber));

    private string BuildPassDownloadUrl(string serialNumber)
    {
        var baseUrl = _options.WebServiceURL?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = $"{Request.Scheme}://{Request.Host}";

        var path = _environment.IsDevelopment()
            ? $"/api/dev/passes/{Uri.EscapeDataString(serialNumber)}"
            : $"/api/passes/{Uri.EscapeDataString(serialNumber)}";

        return $"{baseUrl}{path}";
    }

    public sealed record PublicJoinRequest(
        string FirstName,
        string LastName,
        string Phone);

    public sealed record PublicJoinResponse(
        Guid CustomerId,
        string SerialNumber,
        string FullName,
        string Phone,
        bool AlreadyExists,
        string Message,
        string PassDownloadUrl);

    public sealed record PublicBirthdayRequest(int Day, int Month);

    public sealed record PublicBirthdayResponse(int Day, int Month, string Message);
}
